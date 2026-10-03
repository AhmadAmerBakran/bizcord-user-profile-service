namespace UserProfile.Domain.Entities;

public sealed class UserMessageActivity
{
    public Guid UserId { get; }
    public int MessageCount { get; }
    public DateTime LastMessageAt { get; }

    private UserMessageActivity(Guid userId, int messageCount, DateTime lastMessageAt)
    {
        UserId = userId;
        MessageCount = messageCount;
        LastMessageAt = lastMessageAt;
    }

    public static UserMessageActivity FromFirstMessage(Guid userId, DateTime postedAt)
    {
        Validate(userId, postedAt);
        return new UserMessageActivity(userId, 1, postedAt);
    }

    public UserMessageActivity RecordMessage(DateTime postedAt)
    {
        if (postedAt == default)
        {
            throw new ArgumentException("Posted time cannot be empty.", nameof(postedAt));
        }

        var lastMessageAt = postedAt > LastMessageAt ? postedAt : LastMessageAt;
        return new UserMessageActivity(UserId, checked(MessageCount + 1), lastMessageAt);
    }

    private static void Validate(Guid userId, DateTime postedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (postedAt == default)
        {
            throw new ArgumentException("Posted time cannot be empty.", nameof(postedAt));
        }
    }
}
