using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;

namespace PremiumSagaDemo;

public sealed class ParticipantState
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public bool Charged { get; set; }
    public bool Refunded { get; set; }
    public bool Reversed { get; set; }
    public bool Activated { get; set; }
    public bool Deactivated { get; set; }
    public bool Welcomed { get; set; }
    public bool Corrected { get; set; }
}

// Each role runs in its own process and owns its own saved participant state.
public sealed class ParticipantProcessor
{
    private readonly string _role;
    private readonly string _failStep;
    private readonly JsonStateStore<ParticipantState> _store;
    private readonly IMessageClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ParticipantProcessor(
        string role,
        string? failStep,
        JsonStateStore<ParticipantState> store,
        IMessageClient client)
    {
        _role = role.ToLowerInvariant();
        _failStep = failStep?.ToLowerInvariant() ?? "";
        _store = store;
        _client = client;
    }

    public async Task HandleAsync(object message, CancellationToken token = default)
    {
        var (sagaId, userId) = message switch
        {
            ChargePremium m => (m.SagaId, m.UserId),
            RefundPremium m => (m.SagaId, m.UserId),
            ReportPremiumChargeReversal m => (m.SagaId, m.UserId),
            ActivatePremium m => (m.SagaId, m.UserId),
            DeactivatePremium m => (m.SagaId, m.UserId),
            SendPremiumWelcome m => (m.SagaId, m.UserId),
            SendPremiumCorrection m => (m.SagaId, m.UserId),
            _ => throw new ArgumentOutOfRangeException(nameof(message))
        };

        if (sagaId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Saga and user IDs are required.");
        }

        await _gate.WaitAsync(token);
        try
        {
            var state = await _store.GetAsync(sagaId, token) ?? new ParticipantState
            {
                SagaId = sagaId,
                UserId = userId
            };
            if (state.UserId != userId)
            {
                return;
            }

            object? result = message switch
            {
                ChargePremium m when _role == "payment" => Charge(m, state),
                RefundPremium m when _role == "payment" => Refund(m, state),
                ReportPremiumChargeReversal m when _role == "payment" => Reverse(m, state),
                ActivatePremium m when _role == "subscription" => Activate(m, state),
                DeactivatePremium m when _role == "subscription" => Deactivate(m, state),
                SendPremiumWelcome m when _role == "notification" => Welcome(m, state),
                SendPremiumCorrection m when _role == "notification" => Correct(m, state),
                _ => throw new InvalidOperationException("Command does not belong to " + _role)
            };

            await _store.SaveAsync(sagaId, state, token);
            if (result is not null)
            {
                await PremiumSagaPublisher.PublishAsync(_client, result, token);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private object Charge(ChargePremium message, ParticipantState state)
    {
        if (message.Amount <= 0 || state.Refunded || state.Reversed || _failStep == "charge")
        {
            return new PremiumChargeFailed(message.SagaId, message.UserId, "Payment was declined.");
        }

        state.Charged = true;
        return new PremiumCharged(message.SagaId, message.UserId);
    }

    private object Refund(RefundPremium message, ParticipantState state)
    {
        if (_failStep == "refund")
        {
            return new PremiumRefundFailed(message.SagaId, message.UserId, "The refund could not be completed.");
        }

        if (!state.Reversed)
        {
            state.Refunded = true;
            state.Charged = false;
        }

        return new PremiumRefunded(message.SagaId, message.UserId);
    }

    private object? Reverse(ReportPremiumChargeReversal message, ParticipantState state)
    {
        if (!state.Charged && !state.Reversed)
        {
            return null;
        }

        state.Charged = false;
        state.Reversed = true;
        return new PremiumChargeReversed(message.SagaId, message.UserId, message.Reason);
    }

    private object Activate(ActivatePremium message, ParticipantState state)
    {
        if (state.Deactivated || _failStep == "activate")
        {
            return new PremiumActivationFailed(message.SagaId, message.UserId, "Subscription activation failed.");
        }

        state.Activated = true;
        return new PremiumActivated(message.SagaId, message.UserId);
    }

    private object Deactivate(DeactivatePremium message, ParticipantState state)
    {
        if (_failStep == "deactivate")
        {
            return new PremiumDeactivationFailed(message.SagaId, message.UserId, "Subscription deactivation failed.");
        }

        state.Deactivated = true;
        state.Activated = false;
        return new PremiumDeactivated(message.SagaId, message.UserId);
    }

    private object Welcome(SendPremiumWelcome message, ParticipantState state)
    {
        if (state.Corrected || _failStep == "welcome")
        {
            return new PremiumWelcomeFailed(message.SagaId, message.UserId, "The welcome notification failed.");
        }

        state.Welcomed = true;
        return new PremiumWelcomeSent(message.SagaId, message.UserId);
    }

    private object Correct(SendPremiumCorrection message, ParticipantState state)
    {
        if (_failStep == "correction")
        {
            return new PremiumCorrectionFailed(message.SagaId, message.UserId, "The correction could not be delivered.");
        }

        // The first notice remains delivered. A later correction is a new message.
        state.Corrected = true;
        return new PremiumCorrectionSent(message.SagaId, message.UserId);
    }
}
