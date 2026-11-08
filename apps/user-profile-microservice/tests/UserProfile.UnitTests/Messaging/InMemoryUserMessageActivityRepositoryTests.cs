using FluentAssertions;
using UserProfile.Infrastructure.Repositories;
using Xunit;

namespace UserProfile.UnitTests.Messaging;

public sealed class InMemoryUserMessageActivityRepositoryTests
{
    [Fact]
    public void RecordMessage_RepeatedIdDoesNotChangeCountOrLastMessageTime()
    {
        var repository = new InMemoryUserMessageActivityRepository();
        var authorId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var firstPostedAt = DateTime.UtcNow.AddMinutes(-5);

        repository.RecordMessage(authorId, messageId, firstPostedAt);
        var replay = repository.RecordMessage(authorId, messageId, DateTime.UtcNow);

        replay.MessageCount.Should().Be(1);
        replay.LastMessageAt.Should().Be(firstPostedAt);
    }

    [Fact]
    public void RecordMessage_RejectsSameMessageIdForDifferentAuthors()
    {
        var repository = new InMemoryUserMessageActivityRepository();
        var messageId = Guid.NewGuid();
        repository.RecordMessage(Guid.NewGuid(), messageId, DateTime.UtcNow);

        Action replay = () => repository.RecordMessage(Guid.NewGuid(), messageId, DateTime.UtcNow);

        replay.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task RecordMessage_ConcurrentDuplicatesAreCountedOnce()
    {
        var repository = new InMemoryUserMessageActivityRepository();
        var authorId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var postedAt = DateTime.UtcNow;

        var writes = Enumerable.Range(0, 25).Select(_ => Task.Run(() =>
            repository.RecordMessage(authorId, messageId, postedAt)));

        await Task.WhenAll(writes);

        repository.GetByUserId(authorId)!.MessageCount.Should().Be(1);
    }
}
