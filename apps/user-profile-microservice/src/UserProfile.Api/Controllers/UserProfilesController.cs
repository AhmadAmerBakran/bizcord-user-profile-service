using Microsoft.AspNetCore.Mvc;
using UserProfile.Api.Models;
using UserProfile.Application.Exceptions;
using UserProfile.Application.Services;
using UserProfile.Contracts.Profiles;

namespace UserProfile.Api.Controllers;

[ApiController]
[Route("api/user-profiles")]
public sealed class UserProfilesController : ControllerBase
{
    private readonly IUserProfileService _service;

    public UserProfilesController(IUserProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<UserProfileDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<UserProfileDto>> GetAll()
    {
        return Ok(_service.GetAll());
    }

    [HttpGet("{userId:guid}", Name = nameof(GetByUserId))]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<UserProfileDto> GetByUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return InvalidUserId();
        }

        var profile = _service.GetByUserId(userId);
        return profile is null ? ProfileNotFound(userId) : Ok(profile);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public ActionResult<UserProfileDto> Create(CreateUserProfileRequest request)
    {
        if (request.UserId is null || request.UserId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(request.UserId), "User id cannot be empty.");
            return ValidationProblem(ModelState);
        }

        try
        {
            var profile = _service.Create(
                request.UserId.Value,
                request.DisplayName!,
                request.Bio);

            return CreatedAtRoute(
                nameof(GetByUserId),
                new { userId = profile.UserId },
                profile);
        }
        catch (UserProfileAlreadyExistsException exception)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "User profile already exists",
                Detail = exception.Message
            });
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("profile", exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpPut("{userId:guid}")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public ActionResult<UserProfileDto> Update(Guid userId, UpdateUserProfileRequest request)
    {
        if (userId == Guid.Empty)
        {
            return InvalidUserId();
        }

        try
        {
            var profile = _service.Update(userId, request.DisplayName!, request.Bio);
            return profile is null ? ProfileNotFound(userId) : Ok(profile);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError("profile", exception.Message);
            return ValidationProblem(ModelState);
        }
    }

    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return InvalidUserId();
        }

        return _service.Delete(userId) ? NoContent() : ProfileNotFound(userId);
    }

    private BadRequestObjectResult InvalidUserId()
    {
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid user id",
            Detail = "User id cannot be empty."
        });
    }

    private NotFoundObjectResult ProfileNotFound(Guid userId)
    {
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "User profile not found",
            Detail = $"No profile was found for user '{userId}'."
        });
    }
}
