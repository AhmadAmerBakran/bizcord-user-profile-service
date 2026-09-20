namespace UserProfile.Application.Exceptions;

public sealed class UserProfileAlreadyExistsException : Exception
{
    public UserProfileAlreadyExistsException(Guid userId)
        : base($"A profile already exists for user '{userId}'.")
    {
    }
}
