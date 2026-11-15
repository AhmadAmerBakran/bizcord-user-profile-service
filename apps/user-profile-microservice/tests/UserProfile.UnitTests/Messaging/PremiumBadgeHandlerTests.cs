using FluentAssertions;
using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;
using UserProfile.Application.Services;
using UserProfile.Infrastructure.Repositories;
using Xunit;

namespace UserProfile.UnitTests.Messaging;

public sealed class PremiumBadgeHandlerTests
{
    [Fact]
    public async Task ApplyingAndCompensatingBadge_IsIdempotent()
    {
        var profiles = new UserProfileService(new InMemoryUserProfileRepository());
        var userId = Guid.NewGuid();
        var sagaId = Guid.NewGuid();
        profiles.Create(userId, "Premium member", null);

        var client = new MessageCapture();
        var handler = new PremiumBadgeHandler(profiles, new PremiumBadgeRegistry(), client);

        await handler.HandleAsync(new ApplyPremiumBadge(sagaId, userId), CancellationToken.None);
        await handler.HandleAsync(new ApplyPremiumBadge(sagaId, userId), CancellationToken.None);
        profiles.GetByUserId(userId)!.IsPremium.Should().BeTrue();

        await handler.HandleAsync(new RemovePremiumBadge(sagaId, userId), CancellationToken.None);
        await handler.HandleAsync(new ApplyPremiumBadge(sagaId, userId), CancellationToken.None);

        profiles.GetByUserId(userId)!.IsPremium.Should().BeFalse();
        client.Published.OfType<PremiumBadgeApplied>().Should().HaveCount(2);
        client.Published.OfType<PremiumBadgeRemoved>().Should().ContainSingle();
        client.Published.Last().Should().BeOfType<PremiumBadgeFailed>();
    }

    [Fact]
    public async Task MissingProfile_ReportsFailureWithoutCreatingAnAccount()
    {
        var profiles = new UserProfileService(new InMemoryUserProfileRepository());
        var client = new MessageCapture();
        var handler = new PremiumBadgeHandler(profiles, new PremiumBadgeRegistry(), client);
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new ApplyPremiumBadge(Guid.NewGuid(), userId), CancellationToken.None);

        profiles.GetByUserId(userId).Should().BeNull();
        client.Published.Should().ContainSingle().Which.Should().BeOfType<PremiumBadgeFailed>();
    }

    private sealed class MessageCapture : IMessageClient
    {
        public List<object> Published { get; } = new();

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            Published.Add(message!);
            return Task.CompletedTask;
        }

        public Task<IDisposable> SubscribeAsync<T>(
            string subscriptionId, Func<T, Task> handler, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
