using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using UserProfile.Application.Services;
using UserProfile.IntegrationTests.Infrastructure;
using Xunit;

namespace UserProfile.IntegrationTests.GraphQL;

public sealed class ProfileGraphQLTests : IClassFixture<TestApplicationFactory>
{
    private const string SigningKey = "test-only-jwt-signing-key-for-ci-2026-keep-out-of-production";
    private readonly TestApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProfileGraphQLTests(TestApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQLRequiresAuthentication()
    {
        using var response = await QueryAsync("{ myProfile { displayName } }");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GraphQLRequiresTheProfileReadScope()
    {
        using var response = await QueryAsync(
            "{ myProfile { displayName } }",
            Token(Guid.NewGuid(), "member", "profile.write"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberCanQueryTheirOwnProfileAndChooseFields()
    {
        var userId = CreateProfile("GraphQL member");

        using var response = await QueryAsync(
            "{ myProfile { displayName isPremium } }",
            Token(userId, "member", "profile.read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
        var profile = body.RootElement.GetProperty("data").GetProperty("myProfile");
        Assert.Equal("GraphQL member", profile.GetProperty("displayName").GetString());
        Assert.False(profile.GetProperty("isPremium").GetBoolean());
        Assert.False(profile.TryGetProperty("userId", out _));
        Assert.False(profile.TryGetProperty("bio", out _));
    }

    [Fact]
    public async Task MemberCannotQueryAnotherUsersProfile()
    {
        var otherUserId = CreateProfile("Private profile");
        using var response = await QueryAsync(
            $"{{ userProfile(userId: \"{otherUserId}\") {{ displayName }} }}",
            Token(Guid.NewGuid(), "member", "profile.read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        Assert.Equal("FORBIDDEN", body.RootElement.GetProperty("errors")[0]
            .GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MemberCannotListProfiles()
    {
        using var response = await QueryAsync(
            "{ userProfiles { userId } }",
            Token(Guid.NewGuid(), "member", "profile.read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        Assert.True(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task AdminCanReadProfilesThroughTheExistingApplicationService()
    {
        var userId = CreateProfile("Admin query test");

        using var response = await QueryAsync(
            "{ userProfiles { userId displayName } }",
            Token(Guid.NewGuid(), "admin", "profile.read"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadBodyAsync(response);
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
        var profiles = body.RootElement.GetProperty("data").GetProperty("userProfiles")
            .EnumerateArray().ToArray();
        Assert.Contains(profiles, profile =>
            profile.GetProperty("userId").GetString()?.Equals(userId.ToString(), StringComparison.OrdinalIgnoreCase) == true
            && profile.GetProperty("displayName").GetString() == "Admin query test");
    }

    private Guid CreateProfile(string displayName)
    {
        var userId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IUserProfileService>()
            .Create(userId, displayName, "A private bio");
        return userId;
    }

    private async Task<HttpResponseMessage> QueryAsync(string query, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query })
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string Token(Guid userId, string role, string scope)
    {
        var jwt = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("role", role),
                new Claim("scope", scope)
            },
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
