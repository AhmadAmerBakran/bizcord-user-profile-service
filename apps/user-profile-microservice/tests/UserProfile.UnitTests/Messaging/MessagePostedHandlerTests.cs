using FluentAssertions;
using Shared.Contracts.Events;
using UserProfile.Api.Messaging;
using UserProfile.Application.Abstractions;
using UserProfile.Domain.Entities;
using UserProfile.Infrastructure.Repositories;
using Xunit;

namespace UserProfile.UnitTests.Messaging;

public sealed class MessagePostedHandlerTests
{
    [Fact]
    public async Task HandleAsync_RecordsActivity_AndPublishesResultWithCorrectMessageId()
    {
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var postedAt = DateTime.UtcNow.AddSeconds(-2);
        var repository = new FakeActivityRepository();
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(repository, client);

        await handler.HandleAsync(
            new MessagePostedEvent
            {
                MessageId = messageId,
                ChannelId = Guid.NewGuid(),
                AuthorId = authorId,
                Content = "Hello world",
                PostedAt = postedAt
            },
            CancellationToken.None);

        var activity = repository.GetByUserId(authorId);
        activity.Should().NotBeNull();
        activity!.MessageCount.Should().Be(1);
        activity.LastMessageAt.Should().Be(postedAt);

        var published = client.Published.Should().ContainSingle().Subject
            .Should().BeOfType<UserProfileActivityUpdatedEvent>().Subject;

        published.MessageId.Should().Be(messageId);
        published.UserId.Should().Be(authorId);
        published.MessageCount.Should().Be(1);
        published.LastMessageAt.Should().Be(postedAt);
    }

    [Fact]
    public async Task HandleAsync_Redelivery_DoesNotCountSameMessageTwice()
    {
        var repository = new InMemoryUserMessageActivityRepository();
        var client = new FakeMessageClient();
        var handler = new MessagePostedHandler(repository, client);
        var message = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hi",
            PostedAt = DateTime.UtcNow
        };

        await handler.HandleAsync(message, CancellationToken.None);
        await handler.HandleAsync(message, CancellationToken.None);

        repository.GetByUserId(message.AuthorId)!.MessageCount.Should().Be(1);
        client.Published.Should().HaveCount(2);
        client.Published.Cast<UserProfileActivityUpdatedEvent>()
            .Should().OnlyContain(result => result.MessageId == message.MessageId && result.MessageCount == 1);
    }

    [Fact]
    public async Task HandleAsync_PublishFailure_DoesNotDoubleCountOnReplay()
    {
        var repository = new InMemoryUserMessageActivityRepository();
        var client = new FakeMessageClient { FailNextPublish = true };
        var handler = new MessagePostedHandler(repository, client);
        var message = new MessagePostedEvent
        {
            MessageId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hi",
            PostedAt = DateTime.UtcNow
        };

        Func<Task> firstDelivery = () => handler.HandleAsync(message, CancellationToken.None);
        await firstDelivery.Should().ThrowAsync<IOException>();

        await handler.HandleAsync(message, CancellationToken.None);

        repository.GetByUserId(message.AuthorId)!.MessageCount.Should().Be(1);
        var published = client.Published.Should().ContainSingle().Subject
            .Should().BeOfType<UserProfileActivityUpdatedEvent>().Subject;
        published.MessageId.Should().Be(message.MessageId);
        published.MessageCount.Should().Be(1);
    }

    private sealed class FakeActivityRepository : IUserMessageActivityRepository
    {
        private readonly Dictionary<Guid, UserMessageActivity> _activities = new();

        public UserMessageActivity RecordMessage(Guid userId, Guid messageId, DateTime postedAt)
        {
            var activity = _activities.TryGetValue(userId, out var current)
                ? current.RecordMessage(postedAt)
                : UserMessageActivity.FromFirstMessage(userId, postedAt);

            _activities[userId] = activity;
            return activity;
        }

        public UserMessageActivity? GetByUserId(Guid userId)
        {
            return _activities.TryGetValue(userId, out var activity) ? activity : null;
        }
    }

    private sealed class FakeMessageClient : IMessageClient
    {
        public List<object> Published { get; } = new();
        public bool FailNextPublish { get; set; }

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            if (FailNextPublish)
            {
                FailNextPublish = false;
                throw new IOException("Message broker unavailable.");
            }

            Published.Add(message!);
            return Task.CompletedTask;
        }

        public Task<IDisposable> SubscribeAsync<T>(
            string subscriptionId,
            Func<T, Task> handler,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
