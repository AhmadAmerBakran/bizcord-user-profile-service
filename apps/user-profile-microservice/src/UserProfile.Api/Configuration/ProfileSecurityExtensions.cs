using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace UserProfile.Api.Configuration;

public static class ProfileSecurityExtensions
{
    public static IServiceCollection AddProfileSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["AuthenticationProviderKey"];
        if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
        {
            throw new InvalidOperationException(
                "AuthenticationProviderKey must be supplied securely and contain at least 32 UTF-8 bytes.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    RoleClaimType = "role",
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ProfileRead", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole("member", "admin")
                    .RequireAssertion(context => HasScope(context.User, "profile.read")));

            options.AddPolicy("ProfileAdminRead", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole("admin")
                    .RequireAssertion(context => HasScope(context.User, "profile.read")));

            options.AddPolicy("ProfileAdminWrite", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole("admin")
                    .RequireAssertion(context => HasScope(context.User, "profile.write")));

            options.AddPolicy("InternalProfileRead", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole("internal-service")
                    .RequireAssertion(context => HasScope(context.User, "profile.internal.read")));
        });

        return services;
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string expectedScope)
    {
        return user.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(expectedScope, StringComparer.Ordinal);
    }
}
