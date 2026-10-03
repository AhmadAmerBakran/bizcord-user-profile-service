using FluentAssertions;
using Shared.Contracts.Events;
using UserProfile.Api.Messaging;
using UserProfile.Application.Abstractions;
using UserProfile.Domain.Entities;

namespace UserProfile.ContractTests.Messaging;

public sealed class MessagePostedConsumerContractTests
{
    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        var client = new FakeMessageClient();
        var repository = new FakeActivityRepository();
        var handler = new MessagePostedHandler(repository, client);
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        Func<Task> act = () => handler.HandleAsync(
            new MessagePostedEvent
            {
                MessageId = messageId,
                ChannelId = Guid.NewGuid(),
                AuthorId = authorId,
                Content = "Hello world",
                PostedAt = DateTime.UtcNow
            },
            CancellationToken.None);

        await act.Should().NotThrowAsync();

        var published = client.Published.Should().ContainSingle().Subject
            .Should().BeOfType<UserProfileActivityUpdatedEvent>().Subject;

        published.MessageId.Should().Be(messageId);
        published.UserId.Should().Be(authorId);
        published.MessageCount.Should().Be(1);
        published.LastMessageAt.Should().NotBe(default);
    }

    private sealed class FakeActivityRepository : IUserMessageActivityRepository
    {
        private readonly Dictionary<Guid, UserMessageActivity> _activities = new();

        public UserMessageActivity RecordMessage(Guid userId, DateTime postedAt)
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

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        {
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
