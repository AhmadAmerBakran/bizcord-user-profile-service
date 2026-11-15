using EasyNetQ;
using FluentAssertions;
using PremiumSagaDemo;
using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;
using UserProfile.Application.Services;
using UserProfile.Infrastructure.Repositories;
using Xunit;

namespace PremiumSagaDemoTests;

public sealed class PremiumSagaRabbitMqTests
{
    [Fact]
    public async Task BrokerWorkflow_CompletesAndHandlesDelayedPaymentReversal()
    {
        var connection = Environment.GetEnvironmentVariable("BIZCORD_TEST_RABBITMQ")
            ?? "host=localhost;username=bizcord;password=bizcord";
        using var bus = RabbitHutch.CreateBus(connection);
        var messages = new EasyNetQMessageClient(bus);
        var directory = Path.Combine(Path.GetTempPath(), "week46-broker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var subscriptions = new List<IDisposable>();

        try
        {
            var coordinator = new PremiumSagaCoordinator(
                new JsonStateStore<PremiumSagaState>(Path.Combine(directory, "coordinator.json")), messages);
            var paymentStore = new JsonStateStore<ParticipantState>(Path.Combine(directory, "payment.json"));
            var subscriptionStore = new JsonStateStore<ParticipantState>(Path.Combine(directory, "subscription.json"));
            var notificationStore = new JsonStateStore<ParticipantState>(Path.Combine(directory, "notification.json"));
            var payment = new ParticipantProcessor("payment", null, paymentStore, messages);
            var subscription = new ParticipantProcessor("subscription", null, subscriptionStore, messages);
            var notification = new ParticipantProcessor("notification", null, notificationStore, messages);
            var profiles = new UserProfileService(new InMemoryUserProfileRepository());
            var badge = new PremiumBadgeHandler(profiles, new PremiumBadgeRegistry(), messages);
            var userId = Guid.NewGuid();
            var sagaId = Guid.NewGuid();
            profiles.Create(userId, "Broker test", null);

            var prefix = "week46-broker-" + sagaId.ToString("N");
            async Task Register<T>(Func<T, Task> handler)
            {
                subscriptions.Add(await messages.SubscribeAsync(
                    prefix + "-" + typeof(T).Name, handler));
            }

            await Register<ChargePremium>(msg => payment.HandleAsync(msg));
            await Register<RefundPremium>(msg => payment.HandleAsync(msg));
            await Register<ReportPremiumChargeReversal>(msg => payment.HandleAsync(msg));
            await Register<ActivatePremium>(msg => subscription.HandleAsync(msg));
            await Register<DeactivatePremium>(msg => subscription.HandleAsync(msg));
            await Register<ApplyPremiumBadge>(msg => badge.HandleAsync(msg, CancellationToken.None));
            await Register<RemovePremiumBadge>(msg => badge.HandleAsync(msg, CancellationToken.None));
            await Register<SendPremiumWelcome>(msg => notification.HandleAsync(msg));
            await Register<SendPremiumCorrection>(msg => notification.HandleAsync(msg));

            await Register<PremiumCharged>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumChargeFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumActivated>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumActivationFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumBadgeApplied>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumBadgeFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumWelcomeSent>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumWelcomeFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumBadgeRemoved>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumBadgeRemovalFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumDeactivated>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumDeactivationFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumRefunded>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumRefundFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumCorrectionSent>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumCorrectionFailed>(msg => coordinator.HandleAsync(msg));
            await Register<PremiumChargeReversed>(msg => coordinator.HandleAsync(msg));

            await coordinator.HandleAsync(new StartPremiumPurchase(sagaId, userId, 15m));
            await WaitForPhase(coordinator, sagaId, PremiumSagaPhase.Completed);
            profiles.GetByUserId(userId)!.IsPremium.Should().BeTrue();

            await payment.HandleAsync(
                new ReportPremiumChargeReversal(sagaId, userId, "Settlement rejected"));
            await WaitForPhase(coordinator, sagaId, PremiumSagaPhase.Compensated);

            profiles.GetByUserId(userId)!.IsPremium.Should().BeFalse();
            (await paymentStore.GetAsync(sagaId))!.Reversed.Should().BeTrue();
            (await paymentStore.GetAsync(sagaId))!.Refunded.Should().BeFalse();
            (await subscriptionStore.GetAsync(sagaId))!.Deactivated.Should().BeTrue();
            (await notificationStore.GetAsync(sagaId))!.Corrected.Should().BeTrue();
        }
        finally
        {
            foreach (var disposable in subscriptions)
            {
                disposable.Dispose();
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WaitForPhase(
        PremiumSagaCoordinator coordinator, Guid sagaId, PremiumSagaPhase phase)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var state = await coordinator.GetAsync(sagaId);
            if (state?.Phase == phase)
            {
                return;
            }

            if (state?.Phase == PremiumSagaPhase.AttentionRequired
                || state?.Phase == PremiumSagaPhase.Failed)
            {
                throw new InvalidOperationException("Saga stopped unexpectedly: " + state.FailureReason);
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("The expected saga phase was never reached: " + phase);
    }
}
