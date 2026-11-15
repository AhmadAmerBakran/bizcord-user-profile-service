using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;

namespace PremiumSagaDemo;

// The existing Message Client publishes by concrete message type, not by object.
public static class PremiumSagaPublisher
{
    public static Task PublishAsync(IMessageClient client, object message, CancellationToken cancellationToken)
        => message switch
        {
            StartPremiumPurchase x => client.PublishAsync(x, cancellationToken),
            ChargePremium x => client.PublishAsync(x, cancellationToken),
            PremiumCharged x => client.PublishAsync(x, cancellationToken),
            PremiumChargeFailed x => client.PublishAsync(x, cancellationToken),
            ActivatePremium x => client.PublishAsync(x, cancellationToken),
            PremiumActivated x => client.PublishAsync(x, cancellationToken),
            PremiumActivationFailed x => client.PublishAsync(x, cancellationToken),
            ApplyPremiumBadge x => client.PublishAsync(x, cancellationToken),
            PremiumBadgeApplied x => client.PublishAsync(x, cancellationToken),
            PremiumBadgeFailed x => client.PublishAsync(x, cancellationToken),
            SendPremiumWelcome x => client.PublishAsync(x, cancellationToken),
            PremiumWelcomeSent x => client.PublishAsync(x, cancellationToken),
            PremiumWelcomeFailed x => client.PublishAsync(x, cancellationToken),
            RemovePremiumBadge x => client.PublishAsync(x, cancellationToken),
            PremiumBadgeRemoved x => client.PublishAsync(x, cancellationToken),
            PremiumBadgeRemovalFailed x => client.PublishAsync(x, cancellationToken),
            DeactivatePremium x => client.PublishAsync(x, cancellationToken),
            PremiumDeactivated x => client.PublishAsync(x, cancellationToken),
            PremiumDeactivationFailed x => client.PublishAsync(x, cancellationToken),
            RefundPremium x => client.PublishAsync(x, cancellationToken),
            PremiumRefunded x => client.PublishAsync(x, cancellationToken),
            PremiumRefundFailed x => client.PublishAsync(x, cancellationToken),
            SendPremiumCorrection x => client.PublishAsync(x, cancellationToken),
            PremiumCorrectionSent x => client.PublishAsync(x, cancellationToken),
            PremiumCorrectionFailed x => client.PublishAsync(x, cancellationToken),
            ReportPremiumChargeReversal x => client.PublishAsync(x, cancellationToken),
            PremiumChargeReversed x => client.PublishAsync(x, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.GetType().FullName)
        };
}
