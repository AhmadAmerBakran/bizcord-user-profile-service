using System.Collections.Concurrent;
using UserProfile.Application.Abstractions;
using DomainProfile = UserProfile.Domain.Entities.UserProfile;

namespace UserProfile.Infrastructure.Repositories;

public sealed class InMemoryUserProfileRepository : IUserProfileRepository
{
    private readonly ConcurrentDictionary<Guid, DomainProfile> _profiles = new();

    public IReadOnlyCollection<DomainProfile> GetAll()
    {
        return _profiles.Values
            .OrderBy(profile => profile.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public DomainProfile? GetByUserId(Guid userId)
    {
        return _profiles.TryGetValue(userId, out var profile) ? profile : null;
    }

    public bool TryAdd(DomainProfile profile)
    {
        return _profiles.TryAdd(profile.UserId, profile);
    }

    public void Update(DomainProfile profile)
    {
        _profiles[profile.UserId] = profile;
    }

    public bool Delete(Guid userId)
    {
        return _profiles.TryRemove(userId, out _);
    }
}
