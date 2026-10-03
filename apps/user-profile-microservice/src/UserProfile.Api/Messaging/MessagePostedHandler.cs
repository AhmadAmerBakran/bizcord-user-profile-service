using Shared.Contracts.Events;
using UserProfile.Application.Abstractions;

namespace UserProfile.Api.Messaging;

public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly IUserMessageActivityRepository _activityRepository;
    private readonly IMessageClient _messageClient;

    public MessagePostedHandler(
        IUserMessageActivityRepository activityRepository,
        IMessageClient messageClient)
    {
        _activityRepository = activityRepository;
        _messageClient = messageClient;
    }

    public async Task HandleAsync(
        MessagePostedEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.MessageId == Guid.Empty)
        {
            throw new ArgumentException("Message id cannot be empty.", nameof(message));
        }

        if (message.AuthorId == Guid.Empty)
        {
            throw new ArgumentException("Author id cannot be empty.", nameof(message));
        }

        if (message.PostedAt == default)
        {
            throw new ArgumentException("Posted time cannot be empty.", nameof(message));
        }

        var activity = _activityRepository.RecordMessage(message.AuthorId, message.PostedAt);

        await _messageClient.PublishAsync(
            new UserProfileActivityUpdatedEvent
            {
                MessageId = message.MessageId,
                UserId = activity.UserId,
                MessageCount = activity.MessageCount,
                LastMessageAt = activity.LastMessageAt,
                ProcessedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}
