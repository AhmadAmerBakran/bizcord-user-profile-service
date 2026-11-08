using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserProfile.Application.Services;
using UserProfile.Contracts.Profiles;

namespace UserProfile.Api.Controllers;

[ApiController]
[Route("internal/user-profiles")]
[Authorize(Policy = "InternalProfileRead")]
public sealed class InternalUserProfilesController : ControllerBase
{
    private readonly IUserProfileService _service;

    public InternalUserProfilesController(IUserProfileService service)
    {
        _service = service;
    }

    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<UserProfileDto> GetByUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest("User id cannot be empty.");
        }

        var profile = _service.GetByUserId(userId);
        return profile is null ? NotFound() : Ok(profile);
    }
}
