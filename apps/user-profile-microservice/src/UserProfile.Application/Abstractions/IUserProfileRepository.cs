using DomainProfile = UserProfile.Domain.Entities.UserProfile;

namespace UserProfile.Application.Abstractions;

public interface IUserProfileRepository
{
    IReadOnlyCollection<DomainProfile> GetAll();
    DomainProfile? GetByUserId(Guid userId);
    bool TryAdd(DomainProfile profile);
    void Update(DomainProfile profile);
    bool Delete(Guid userId);
}
