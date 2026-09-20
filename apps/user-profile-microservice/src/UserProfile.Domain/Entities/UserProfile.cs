using UserProfile.Domain.ValueObjects;

namespace UserProfile.Domain.Entities;

public sealed class UserProfile
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public DisplayName DisplayName { get; private set; }
    public Bio Bio { get; private set; }

    public UserProfile(Guid id, Guid userId, DisplayName displayName, Bio? bio = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Profile id cannot be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        Id = id;
        UserId = userId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Bio = bio ?? Bio.Empty;
    }

    public void ChangeDisplayName(DisplayName displayName)
    {
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    public void ChangeBio(Bio bio)
    {
        Bio = bio ?? throw new ArgumentNullException(nameof(bio));
    }
}
