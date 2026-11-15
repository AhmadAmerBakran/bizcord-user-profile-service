using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;

namespace PremiumSagaDemo;

public sealed class PremiumSagaCoordinator
{
    private readonly JsonStateStore<PremiumSagaState> _store;
    private readonly IMessageClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public PremiumSagaCoordinator(JsonStateStore<PremiumSagaState> store, IMessageClient client)
    {
        _store = store;
        _client = client;
    }

    public Task<PremiumSagaState?> GetAsync(Guid sagaId, CancellationToken token = default)
        => _store.GetAsync(sagaId, token);

    public async Task HandleAsync(object message, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var sagaId = GetSagaId(message);
        var userId = GetUserId(message);
        if (sagaId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Saga and user IDs are required.");
        }

        await _gate.WaitAsync(token);
        try
        {
            var state = await _store.GetAsync(sagaId, token);
            object? command = null;

            if (message is StartPremiumPurchase start)
            {
                if (start.Amount <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(start), "The amount must be positive.");
                }

                if (state is not null)
                {
                    return;
                }

                state = new PremiumSagaState
                {
                    SagaId = start.SagaId,
                    UserId = start.UserId,
                    Amount = start.Amount,
                    Phase = PremiumSagaPhase.ChargePending,
                    StartedAt = DateTimeOffset.UtcNow
                };
                state.History.Add("Purchase requested");
                command = state.PendingCommand();
            }
            else
            {
                if (state is null || state.UserId != userId)
                {
                    return;
                }

                switch (message)
                {
                    case PremiumCharged when state.Phase == PremiumSagaPhase.ChargePending:
                        state.PaymentCharged = true;
                        state.Phase = PremiumSagaPhase.ActivationPending;
                        state.History.Add("Payment accepted");
                        command = state.PendingCommand();
                        break;

                    case PremiumChargeFailed failure when state.Phase == PremiumSagaPhase.ChargePending:
                        state.FailureReason = failure.Reason;
                        state.Phase = PremiumSagaPhase.Failed;
                        state.FinishedAt = DateTimeOffset.UtcNow;
                        state.History.Add("Payment declined");
                        break;

                    case PremiumActivated when state.Phase == PremiumSagaPhase.ActivationPending:
                        state.SubscriptionActive = true;
                        state.Phase = PremiumSagaPhase.BadgePending;
                        state.History.Add("Subscription activated");
                        command = state.PendingCommand();
                        break;

                    case PremiumBadgeApplied when state.Phase == PremiumSagaPhase.BadgePending:
                        state.BadgeApplied = true;
                        state.Phase = PremiumSagaPhase.WelcomePending;
                        state.History.Add("Profile badge applied");
                        command = state.PendingCommand();
                        break;

                    case PremiumWelcomeSent when state.Phase == PremiumSagaPhase.WelcomePending:
                        state.WelcomeSent = true;
                        state.WasCompleted = true;
                        state.Phase = PremiumSagaPhase.Completed;
                        state.FinishedAt = DateTimeOffset.UtcNow;
                        state.History.Add("Welcome delivered; purchase completed");
                        break;

                    case PremiumActivationFailed failure when state.Phase == PremiumSagaPhase.ActivationPending:
                        command = StartCompensation(state, failure.Reason);
                        break;

                    case PremiumBadgeFailed failure when state.Phase == PremiumSagaPhase.BadgePending:
                        command = StartCompensation(state, failure.Reason);
                        break;

                    case PremiumWelcomeFailed failure when state.Phase == PremiumSagaPhase.WelcomePending:
                        command = StartCompensation(state, failure.Reason);
                        break;

                    case PremiumBadgeRemoved when state.Phase == PremiumSagaPhase.BadgeRemovalPending:
                        state.BadgeApplied = false;
                        state.History.Add("Profile badge removed");
                        command = NextCompensation(state);
                        break;

                    case PremiumDeactivated when state.Phase == PremiumSagaPhase.DeactivationPending:
                        state.SubscriptionActive = false;
                        state.History.Add("Subscription deactivated");
                        command = NextCompensation(state);
                        break;

                    case PremiumRefunded when state.Phase == PremiumSagaPhase.RefundPending:
                        state.PaymentCharged = false;
                        state.History.Add("Payment refunded");
                        command = NextCompensation(state);
                        break;

                    case PremiumCorrectionSent when state.Phase == PremiumSagaPhase.CorrectionPending:
                        state.WelcomeSent = false;
                        state.History.Add("Correction sent; the original welcome cannot be unsent");
                        command = NextCompensation(state);
                        break;

                    case PremiumChargeReversed reversal when state.Phase == PremiumSagaPhase.Completed
                                                               && state.PaymentCharged:
                        state.PaymentCharged = false;
                        state.History.Add("Payment provider reversed its earlier approval");
                        command = StartCompensation(state, reversal.Reason);
                        break;

                    case PremiumChargeReversed reversal when state.PaymentCharged
                                                               && state.Phase != PremiumSagaPhase.Compensated
                                                               && state.Phase != PremiumSagaPhase.Failed
                                                               && state.Phase != PremiumSagaPhase.AttentionRequired:
                        state.PaymentCharged = false;
                        state.FailureReason = reversal.Reason;
                        state.Phase = PremiumSagaPhase.AttentionRequired;
                        state.History.Add("Payment reversed during an in-flight operation; operator intervention required");
                        break;

                    case PremiumBadgeRemovalFailed failure when state.Phase == PremiumSagaPhase.BadgeRemovalPending:
                        RequireAttention(state, failure.Reason);
                        break;

                    case PremiumDeactivationFailed failure when state.Phase == PremiumSagaPhase.DeactivationPending:
                        RequireAttention(state, failure.Reason);
                        break;

                    case PremiumRefundFailed failure when state.Phase == PremiumSagaPhase.RefundPending:
                        RequireAttention(state, failure.Reason);
                        break;

                    case PremiumCorrectionFailed failure when state.Phase == PremiumSagaPhase.CorrectionPending:
                        RequireAttention(state, failure.Reason);
                        break;

                    default:
                        return;
                }
            }

            await _store.SaveAsync(state.SagaId, state, token);
            if (command is not null)
            {
                await PremiumSagaPublisher.PublishAsync(_client, command, token);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static object? StartCompensation(PremiumSagaState state, string reason)
    {
        state.FailureReason = reason;
        state.History.Add("Compensation started: " + reason);
        return NextCompensation(state);
    }

    private static object? NextCompensation(PremiumSagaState state)
    {
        if (state.BadgeApplied)
        {
            state.Phase = PremiumSagaPhase.BadgeRemovalPending;
        }
        else if (state.SubscriptionActive)
        {
            state.Phase = PremiumSagaPhase.DeactivationPending;
        }
        else if (state.PaymentCharged)
        {
            state.Phase = PremiumSagaPhase.RefundPending;
        }
        else if (state.WelcomeSent)
        {
            state.Phase = PremiumSagaPhase.CorrectionPending;
        }
        else
        {
            state.Phase = PremiumSagaPhase.Compensated;
            state.FinishedAt = DateTimeOffset.UtcNow;
            state.History.Add("Compensation completed");
        }

        return state.PendingCommand();
    }

    private static void RequireAttention(PremiumSagaState state, string reason)
    {
        state.Phase = PremiumSagaPhase.AttentionRequired;
        state.FinishedAt = DateTimeOffset.UtcNow;
        state.History.Add("Operator action required: " + reason);
    }

    private static Guid GetSagaId(object message) => message switch
    {
        StartPremiumPurchase x => x.SagaId,
        PremiumCharged x => x.SagaId,
        PremiumChargeFailed x => x.SagaId,
        PremiumActivated x => x.SagaId,
        PremiumActivationFailed x => x.SagaId,
        PremiumBadgeApplied x => x.SagaId,
        PremiumBadgeFailed x => x.SagaId,
        PremiumWelcomeSent x => x.SagaId,
        PremiumWelcomeFailed x => x.SagaId,
        PremiumBadgeRemoved x => x.SagaId,
        PremiumBadgeRemovalFailed x => x.SagaId,
        PremiumDeactivated x => x.SagaId,
        PremiumDeactivationFailed x => x.SagaId,
        PremiumRefunded x => x.SagaId,
        PremiumRefundFailed x => x.SagaId,
        PremiumCorrectionSent x => x.SagaId,
        PremiumCorrectionFailed x => x.SagaId,
        PremiumChargeReversed x => x.SagaId,
        _ => throw new ArgumentOutOfRangeException(nameof(message))
    };

    private static Guid GetUserId(object message) => message switch
    {
        StartPremiumPurchase x => x.UserId,
        PremiumCharged x => x.UserId,
        PremiumChargeFailed x => x.UserId,
        PremiumActivated x => x.UserId,
        PremiumActivationFailed x => x.UserId,
        PremiumBadgeApplied x => x.UserId,
        PremiumBadgeFailed x => x.UserId,
        PremiumWelcomeSent x => x.UserId,
        PremiumWelcomeFailed x => x.UserId,
        PremiumBadgeRemoved x => x.UserId,
        PremiumBadgeRemovalFailed x => x.UserId,
        PremiumDeactivated x => x.UserId,
        PremiumDeactivationFailed x => x.UserId,
        PremiumRefunded x => x.UserId,
        PremiumRefundFailed x => x.UserId,
        PremiumCorrectionSent x => x.UserId,
        PremiumCorrectionFailed x => x.UserId,
        PremiumChargeReversed x => x.UserId,
        _ => throw new ArgumentOutOfRangeException(nameof(message))
    };
}
