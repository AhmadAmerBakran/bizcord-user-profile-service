using System.ComponentModel.DataAnnotations;

namespace UserProfile.Api.Models;

public sealed class CreateUserProfileRequest
{
    [Required]
    public Guid? UserId { get; init; }

    [Required]
    public string? DisplayName { get; init; }

    public string? Bio { get; init; }
}
