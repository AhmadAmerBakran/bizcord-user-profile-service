using UserProfile.Domain.Entities;

namespace UserProfile.Application.Abstractions;

public interface IUserMessageActivityRepository
{
    UserMessageActivity RecordMessage(Guid userId, DateTime postedAt);
    UserMessageActivity? GetByUserId(Guid userId);
}
