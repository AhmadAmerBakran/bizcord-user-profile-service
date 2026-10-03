using System.Collections.Concurrent;
using UserProfile.Application.Abstractions;
using UserProfile.Domain.Entities;

namespace UserProfile.Infrastructure.Repositories;

public sealed class InMemoryUserMessageActivityRepository : IUserMessageActivityRepository
{
    private readonly ConcurrentDictionary<Guid, UserMessageActivity> _activities = new();

    public UserMessageActivity RecordMessage(Guid userId, DateTime postedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (postedAt == default)
        {
            throw new ArgumentException("Posted time cannot be empty.", nameof(postedAt));
        }

        return _activities.AddOrUpdate(
            userId,
            _ => UserMessageActivity.FromFirstMessage(userId, postedAt),
            (_, current) => current.RecordMessage(postedAt));
    }

    public UserMessageActivity? GetByUserId(Guid userId)
    {
        return _activities.TryGetValue(userId, out var activity) ? activity : null;
    }
}
