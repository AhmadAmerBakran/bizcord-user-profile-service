using System.ComponentModel.DataAnnotations;

namespace UserProfile.Api.Models;

public sealed class UpdateUserProfileRequest
{
    [Required]
    public string? DisplayName { get; init; }

    public string? Bio { get; init; }
}
