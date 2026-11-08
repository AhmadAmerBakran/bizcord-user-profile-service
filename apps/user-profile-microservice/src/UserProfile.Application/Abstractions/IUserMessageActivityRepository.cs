using UserProfile.Domain.Entities;

namespace UserProfile.Application.Abstractions;

public interface IUserMessageActivityRepository
{
    UserMessageActivity RecordMessage(Guid userId, Guid messageId, DateTime postedAt);
    UserMessageActivity? GetByUserId(Guid userId);
}
