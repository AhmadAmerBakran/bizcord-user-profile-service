using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Shared.Contracts.Events;
using UserProfile.Api.Messaging;
using UserProfile.IntegrationTests.Infrastructure;
using Xunit;

namespace UserProfile.IntegrationTests.Messaging;

public sealed class MessagePostedIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public MessagePostedIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
    {
        _ = _factory.CreateClient();
        var messageClient = _factory.Services.GetRequiredService<IMessageClient>();
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postedAt = DateTime.UtcNow.AddSeconds(-1);

        await using var capture = await MessageCapture<UserProfileActivityUpdatedEvent>.StartAsync(
            messageClient,
            $"user-profile-integration-{Guid.NewGuid():N}");

        await messageClient.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = authorId,
            Content = "Hello world",
            PostedAt = postedAt
        });

        var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(10));

        result.MessageId.Should().Be(messageId);
        result.UserId.Should().Be(authorId);
        result.MessageCount.Should().Be(1);
        result.LastMessageAt.Should().Be(postedAt);
        result.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }
}
