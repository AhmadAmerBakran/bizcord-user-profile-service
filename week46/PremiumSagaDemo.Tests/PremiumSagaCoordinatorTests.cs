using FluentAssertions;
using PremiumSagaDemo;
using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;
using Xunit;

namespace PremiumSagaDemoTests;

public sealed class PremiumSagaCoordinatorTests
{
    [Fact]
    public async Task SuccessfulPurchase_ExecutesAllFourServices()
    {
        using var rig = new Scenario();
        await rig.StartAsync();
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumActivated(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumBadgeApplied(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumWelcomeSent(rig.SagaId, rig.UserId));

        var state = await rig.StateAsync();
        state.Phase.Should().Be(PremiumSagaPhase.Completed);
        state.WasCompleted.Should().BeTrue();
        state.PaymentCharged.Should().BeTrue();
        state.SubscriptionActive.Should().BeTrue();
        state.BadgeApplied.Should().BeTrue();
        state.WelcomeSent.Should().BeTrue();
        rig.Messages.Published.Select(x => x.GetType()).Should().ContainInOrder(
            typeof(ChargePremium),
            typeof(ActivatePremium),
            typeof(ApplyPremiumBadge),
            typeof(SendPremiumWelcome));
    }

    [Fact]
    public async Task ActivationFailure_RefundsCharge()
    {
        using var rig = new Scenario();
        await rig.StartAsync();
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(
            new PremiumActivationFailed(rig.SagaId, rig.UserId, "Subscription unavailable"));

        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.RefundPending);
        rig.Messages.Published.Last().Should().BeOfType<RefundPremium>();

        await rig.Coordinator.HandleAsync(new PremiumRefunded(rig.SagaId, rig.UserId));
        var state = await rig.StateAsync();
        state.Phase.Should().Be(PremiumSagaPhase.Compensated);
        state.PaymentCharged.Should().BeFalse();
    }

    [Fact]
    public async Task BadgeFailure_DeactivatesSubscriptionBeforeRefunding()
    {
        using var rig = new Scenario();
        await rig.StartAsync();
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumActivated(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumBadgeFailed(rig.SagaId, rig.UserId, "Missing profile"));
        rig.Messages.Published.Last().Should().BeOfType<DeactivatePremium>();
        await rig.Coordinator.HandleAsync(new PremiumDeactivated(rig.SagaId, rig.UserId));
        rig.Messages.Published.Last().Should().BeOfType<RefundPremium>();
        await rig.Coordinator.HandleAsync(new PremiumRefunded(rig.SagaId, rig.UserId));
        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.Compensated);
    }

    [Fact]
    public async Task WelcomeFailure_ReversesBadgeSubscriptionAndPayment()
    {
        using var rig = new Scenario();
        await rig.CompleteUntilWelcomeAsync();
        await rig.Coordinator.HandleAsync(new PremiumWelcomeFailed(rig.SagaId, rig.UserId, "Delivery failed"));
        rig.Messages.Published.Last().Should().BeOfType<RemovePremiumBadge>();
        await rig.Coordinator.HandleAsync(new PremiumBadgeRemoved(rig.SagaId, rig.UserId));
        rig.Messages.Published.Last().Should().BeOfType<DeactivatePremium>();
        await rig.Coordinator.HandleAsync(new PremiumDeactivated(rig.SagaId, rig.UserId));
        rig.Messages.Published.Last().Should().BeOfType<RefundPremium>();
        await rig.Coordinator.HandleAsync(new PremiumRefunded(rig.SagaId, rig.UserId));
        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.Compensated);
    }

    [Fact]
    public async Task LatePaymentReversal_CompensatesCompletedSagaAndSendsCorrection()
    {
        using var rig = new Scenario();
        await rig.CompleteUntilWelcomeAsync();
        await rig.Coordinator.HandleAsync(new PremiumWelcomeSent(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(
            new PremiumChargeReversed(rig.SagaId, rig.UserId, "Provider rejected settlement"));

        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.BadgeRemovalPending);
        await rig.Coordinator.HandleAsync(new PremiumBadgeRemoved(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumDeactivated(rig.SagaId, rig.UserId));
        rig.Messages.Published.Last().Should().BeOfType<SendPremiumCorrection>();
        await rig.Coordinator.HandleAsync(new PremiumCorrectionSent(rig.SagaId, rig.UserId));

        var state = await rig.StateAsync();
        state.Phase.Should().Be(PremiumSagaPhase.Compensated);
        state.WasCompleted.Should().BeTrue();
        state.PaymentCharged.Should().BeFalse();
        state.History.Should().Contain(x => x.Contains("cannot be unsent"));
        rig.Messages.Published.Should().NotContain(x => x is RefundPremium);
    }

    [Fact]
    public async Task DuplicateAndLateEvents_DoNotAdvanceSagaTwice()
    {
        using var rig = new Scenario();
        await rig.StartAsync();
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));
        rig.Messages.Published.OfType<ActivatePremium>().Should().ContainSingle();

        await rig.Coordinator.HandleAsync(new PremiumActivationFailed(rig.SagaId, rig.UserId, "Failure"));
        await rig.Coordinator.HandleAsync(new PremiumRefunded(rig.SagaId, rig.UserId));
        await rig.Coordinator.HandleAsync(new PremiumActivated(rig.SagaId, rig.UserId));
        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.Compensated);
    }

    [Fact]
    public async Task PersistedState_IsAvailableAfterCoordinatorRestart()
    {
        using var rig = new Scenario();
        await rig.StartAsync();
        await rig.Coordinator.HandleAsync(new PremiumCharged(rig.SagaId, rig.UserId));

        var restarted = new PremiumSagaCoordinator(
            new JsonStateStore<PremiumSagaState>(rig.StateFile), new CaptureMessageClient());
        (await restarted.GetAsync(rig.SagaId))!.Phase.Should().Be(PremiumSagaPhase.ActivationPending);
        (await restarted.GetAsync(rig.SagaId))!.History.Should().Contain("Payment accepted");
    }

    [Fact]
    public async Task FailedCompensation_RequestsManualIntervention()
    {
        using var rig = new Scenario();
        await rig.CompleteUntilWelcomeAsync();
        await rig.Coordinator.HandleAsync(new PremiumWelcomeFailed(rig.SagaId, rig.UserId, "Failed"));
        await rig.Coordinator.HandleAsync(new PremiumBadgeRemovalFailed(rig.SagaId, rig.UserId, "Profile missing"));

        (await rig.StateAsync()).Phase.Should().Be(PremiumSagaPhase.AttentionRequired);
        (await rig.StateAsync()).History.Should().Contain(x => x.Contains("Operator action"));
    }

    [Fact]
    public async Task ParticipantRedelivery_DoesNotChargeAgainOrUndoRefund()
    {
        using var rig = new Scenario();
        var store = new JsonStateStore<ParticipantState>(
            Path.Combine(Path.GetDirectoryName(rig.StateFile)!, "payment.json"));
        var processor = new ParticipantProcessor("payment", null, store, rig.Messages);
        var command = new ChargePremium(rig.SagaId, rig.UserId, 12m);

        await processor.HandleAsync(command);
        await processor.HandleAsync(command);
        (await store.GetAsync(rig.SagaId))!.Charged.Should().BeTrue();
        await processor.HandleAsync(new RefundPremium(rig.SagaId, rig.UserId, 12m));
        await processor.HandleAsync(command);

        var state = await store.GetAsync(rig.SagaId);
        state!.Refunded.Should().BeTrue();
        state.Charged.Should().BeFalse();
        rig.Messages.Published.OfType<PremiumCharged>().Should().HaveCount(2);
        rig.Messages.Published.Last().Should().BeOfType<PremiumChargeFailed>();
    }

    private sealed class Scenario : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "premium-saga-tests-" + Guid.NewGuid().ToString("N"));
        public Guid SagaId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public CaptureMessageClient Messages { get; } = new();
        public string StateFile => Path.Combine(_directory, "coordinator.json");
        public PremiumSagaCoordinator Coordinator { get; }

        public Scenario()
        {
            Coordinator = new PremiumSagaCoordinator(
                new JsonStateStore<PremiumSagaState>(StateFile), Messages);
        }

        public Task StartAsync() =>
            Coordinator.HandleAsync(new StartPremiumPurchase(SagaId, UserId, 12m));

        public async Task CompleteUntilWelcomeAsync()
        {
            await StartAsync();
            await Coordinator.HandleAsync(new PremiumCharged(SagaId, UserId));
            await Coordinator.HandleAsync(new PremiumActivated(SagaId, UserId));
            await Coordinator.HandleAsync(new PremiumBadgeApplied(SagaId, UserId));
        }

        public async Task<PremiumSagaState> StateAsync() =>
            (await Coordinator.GetAsync(SagaId))!;

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}

public sealed class CaptureMessageClient : IMessageClient
{
    public List<object> Published { get; } = new();

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        Published.Add(message!);
        return Task.CompletedTask;
    }

    public Task<IDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
