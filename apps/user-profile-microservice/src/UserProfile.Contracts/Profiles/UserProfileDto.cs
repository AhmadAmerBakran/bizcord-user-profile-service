namespace UserProfile.Contracts.Profiles;

public sealed record UserProfileDto(
    Guid UserId,
    string DisplayName,
    string Bio);
