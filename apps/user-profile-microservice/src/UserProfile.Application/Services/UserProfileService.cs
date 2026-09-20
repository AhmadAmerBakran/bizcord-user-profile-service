using UserProfile.Application.Abstractions;
using UserProfile.Application.Exceptions;
using UserProfile.Contracts.Profiles;
using UserProfile.Domain.ValueObjects;
using DomainProfile = UserProfile.Domain.Entities.UserProfile;

namespace UserProfile.Application.Services;

public sealed class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _repository;

    public UserProfileService(IUserProfileRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyCollection<UserProfileDto> GetAll()
    {
        return _repository.GetAll()
            .Select(ToDto)
            .ToArray();
    }

    public UserProfileDto? GetByUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var profile = _repository.GetByUserId(userId);
        return profile is null ? null : ToDto(profile);
    }

    public UserProfileDto Create(Guid userId, string displayName, string? bio)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        var profile = new DomainProfile(
            Guid.NewGuid(),
            userId,
            new DisplayName(displayName),
            new Bio(bio));

        if (!_repository.TryAdd(profile))
        {
            throw new UserProfileAlreadyExistsException(userId);
        }

        return ToDto(profile);
    }

    public UserProfileDto? Update(Guid userId, string displayName, string? bio)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        var profile = _repository.GetByUserId(userId);
        if (profile is null)
        {
            return null;
        }

        profile.ChangeDisplayName(new DisplayName(displayName));
        profile.ChangeBio(new Bio(bio));
        _repository.Update(profile);

        return ToDto(profile);
    }

    public bool Delete(Guid userId)
    {
        return userId != Guid.Empty && _repository.Delete(userId);
    }

    private static UserProfileDto ToDto(DomainProfile profile)
    {
        return new UserProfileDto(
            profile.UserId,
            profile.DisplayName.Value,
            profile.Bio.Value);
    }
}
