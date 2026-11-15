using Shared.Contracts.Sagas;

namespace PremiumSagaDemo;

public enum PremiumSagaPhase
{
    ChargePending,
    ActivationPending,
    BadgePending,
    WelcomePending,
    Completed,
    BadgeRemovalPending,
    DeactivationPending,
    RefundPending,
    CorrectionPending,
    Failed,
    Compensated,
    AttentionRequired
}

public sealed class PremiumSagaState
{
    public Guid SagaId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public PremiumSagaPhase Phase { get; set; }
    public bool PaymentCharged { get; set; }
    public bool SubscriptionActive { get; set; }
    public bool BadgeApplied { get; set; }
    public bool WelcomeSent { get; set; }
    public bool WasCompleted { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public List<string> History { get; set; } = new();

    public object? PendingCommand() => Phase switch
    {
        PremiumSagaPhase.ChargePending => new ChargePremium(SagaId, UserId, Amount),
        PremiumSagaPhase.ActivationPending => new ActivatePremium(SagaId, UserId),
        PremiumSagaPhase.BadgePending => new ApplyPremiumBadge(SagaId, UserId),
        PremiumSagaPhase.WelcomePending => new SendPremiumWelcome(SagaId, UserId),
        PremiumSagaPhase.BadgeRemovalPending => new RemovePremiumBadge(SagaId, UserId),
        PremiumSagaPhase.DeactivationPending => new DeactivatePremium(SagaId, UserId),
        PremiumSagaPhase.RefundPending => new RefundPremium(SagaId, UserId, Amount),
        PremiumSagaPhase.CorrectionPending => new SendPremiumCorrection(SagaId, UserId),
        _ => null
    };
}
