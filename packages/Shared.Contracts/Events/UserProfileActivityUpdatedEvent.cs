namespace Shared.Contracts.Events;

public class UserProfileActivityUpdatedEvent
{
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public int MessageCount { get; set; }
    public DateTime LastMessageAt { get; set; }
    public DateTime ProcessedAt { get; set; }
}
