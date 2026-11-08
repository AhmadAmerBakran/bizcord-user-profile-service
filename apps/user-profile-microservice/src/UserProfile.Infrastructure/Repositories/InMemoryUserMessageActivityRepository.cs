using UserProfile.Application.Abstractions;
using UserProfile.Domain.Entities;

namespace UserProfile.Infrastructure.Repositories;

public sealed class InMemoryUserMessageActivityRepository : IUserMessageActivityRepository
{
    private readonly Dictionary<Guid, UserMessageActivity> _activities = new();
    private readonly Dictionary<Guid, Guid> _processedMessages = new();
    private readonly object _gate = new();

    public UserMessageActivity RecordMessage(Guid userId, Guid messageId, DateTime postedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException("Message id cannot be empty.", nameof(messageId));
        }

        if (postedAt == default)
        {
            throw new ArgumentException("Posted time cannot be empty.", nameof(postedAt));
        }

        lock (_gate)
        {
            if (_processedMessages.TryGetValue(messageId, out var originalUserId))
            {
                if (originalUserId != userId)
                {
                    throw new InvalidOperationException(
                        $"Message '{messageId}' was already recorded for a different user.");
                }

                return _activities[userId];
            }

            var activity = _activities.TryGetValue(userId, out var current)
                ? current.RecordMessage(postedAt)
                : UserMessageActivity.FromFirstMessage(userId, postedAt);

            _activities[userId] = activity;
            _processedMessages.Add(messageId, userId);

            return activity;
        }
    }

    public UserMessageActivity? GetByUserId(Guid userId)
    {
        lock (_gate)
        {
            return _activities.TryGetValue(userId, out var activity) ? activity : null;
        }
    }
}
