using UserProfile.Contracts.Profiles;

namespace UserProfile.Application.Services;

public interface IUserProfileService
{
    IReadOnlyCollection<UserProfileDto> GetAll();
    UserProfileDto? GetByUserId(Guid userId);
    UserProfileDto Create(Guid userId, string displayName, string? bio);
    UserProfileDto? Update(Guid userId, string displayName, string? bio);
    bool SetPremiumStatus(Guid userId, bool isPremium);
    bool Delete(Guid userId);
}
