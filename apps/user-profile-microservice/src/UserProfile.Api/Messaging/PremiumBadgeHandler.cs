using Shared.Contracts.Sagas;
using UserProfile.Application.Services;

namespace UserProfile.Api.Messaging;

// A compensated saga is never allowed to turn the badge back on through a late retry.
public sealed class PremiumBadgeHandler :
    IMessageHandler<ApplyPremiumBadge>,
    IMessageHandler<RemovePremiumBadge>
{
    private readonly IUserProfileService _profiles;
    private readonly PremiumBadgeRegistry _registry;
    private readonly IMessageClient _messages;

    public PremiumBadgeHandler(
        IUserProfileService profiles,
        PremiumBadgeRegistry registry,
        IMessageClient messages)
    {
        _profiles = profiles;
        _registry = registry;
        _messages = messages;
    }

    public Task HandleAsync(ApplyPremiumBadge message, CancellationToken cancellationToken)
    {
        var applied = _registry.Apply(message.SagaId, message.UserId, _profiles);
        return applied
            ? _messages.PublishAsync(new PremiumBadgeApplied(message.SagaId, message.UserId), cancellationToken)
            : _messages.PublishAsync(new PremiumBadgeFailed(message.SagaId, message.UserId,
                "Profile does not exist or this saga was already compensated."), cancellationToken);
    }

    public Task HandleAsync(RemovePremiumBadge message, CancellationToken cancellationToken)
    {
        var removed = _registry.Remove(message.SagaId, message.UserId, _profiles);
        return removed
            ? _messages.PublishAsync(new PremiumBadgeRemoved(message.SagaId, message.UserId), cancellationToken)
            : _messages.PublishAsync(new PremiumBadgeRemovalFailed(message.SagaId, message.UserId,
                "Profile is missing; manual reconciliation is required."), cancellationToken);
    }
}

public sealed class PremiumBadgeRegistry
{
    private readonly object _sync = new();
    private readonly HashSet<Guid> _compensated = new();

    public bool Apply(Guid sagaId, Guid userId, IUserProfileService profiles)
    {
        if (sagaId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            return !_compensated.Contains(sagaId) && profiles.SetPremiumStatus(userId, true);
        }
    }

    public bool Remove(Guid sagaId, Guid userId, IUserProfileService profiles)
    {
        if (sagaId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        lock (_sync)
        {
            if (!profiles.SetPremiumStatus(userId, false))
            {
                return false;
            }

            _compensated.Add(sagaId);
            return true;
        }
    }
}
