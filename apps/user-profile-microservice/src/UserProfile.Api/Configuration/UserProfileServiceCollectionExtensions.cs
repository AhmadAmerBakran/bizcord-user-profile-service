using UserProfile.Application.Abstractions;
using UserProfile.Application.Services;
using UserProfile.Infrastructure.Repositories;

namespace UserProfile.Api.Configuration;

public static class UserProfileServiceCollectionExtensions
{
    public static IServiceCollection AddUserProfiles(this IServiceCollection services)
    {
        services.AddSingleton<IUserProfileRepository, InMemoryUserProfileRepository>();
        services.AddScoped<IUserProfileService, UserProfileService>();

        return services;
    }
}
