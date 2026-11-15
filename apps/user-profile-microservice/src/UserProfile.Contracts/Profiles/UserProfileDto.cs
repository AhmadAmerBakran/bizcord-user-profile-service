namespace UserProfile.Contracts.Profiles;

// Keep the original positional contract; the premium flag is an additive property.
public sealed record UserProfileDto(
    Guid UserId,
    string DisplayName,
    string Bio)
{
    public bool IsPremium { get; init; }
}
