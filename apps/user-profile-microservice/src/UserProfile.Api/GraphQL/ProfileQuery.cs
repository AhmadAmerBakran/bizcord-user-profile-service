using HotChocolate;
using HotChocolate.Authorization;
using UserProfile.Application.Services;
using UserProfile.Contracts.Profiles;

namespace UserProfile.Api.GraphQL;

public sealed class ProfileQuery
{
    [Authorize(Policy = "ProfileRead")]
    public UserProfileDto? GetMyProfile(
        [Service] IUserProfileService profiles,
        [Service] IHttpContextAccessor contextAccessor)
    {
        var subject = contextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The token does not contain a valid user id.")
                    .SetCode("INVALID_USER_ID")
                    .Build());
        }

        return profiles.GetByUserId(userId);
    }

    [Authorize(Policy = "ProfileRead")]
    public UserProfileDto? GetUserProfile(
        Guid userId,
        [Service] IUserProfileService profiles,
        [Service] IHttpContextAccessor contextAccessor)
    {
        if (userId == Guid.Empty)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("User id cannot be empty.")
                    .SetCode("INVALID_USER_ID")
                    .Build());
        }

        var user = contextAccessor.HttpContext?.User;
        var isOwner = Guid.TryParse(user?.FindFirst("sub")?.Value, out var subjectId)
            && subjectId == userId;

        if (user?.IsInRole("admin") != true && !isOwner)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You cannot read another user's profile.")
                    .SetCode("FORBIDDEN")
                    .Build());
        }

        return profiles.GetByUserId(userId);
    }

    [Authorize(Policy = "ProfileAdminRead")]
    public IReadOnlyCollection<UserProfileDto> GetUserProfiles(
        [Service] IUserProfileService profiles) => profiles.GetAll();
}
