using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UserProfile.IntegrationTests.Infrastructure;
using Xunit;

namespace UserProfile.IntegrationTests.Security;

public sealed class ProfileAuthorizationTests : IClassFixture<TestApplicationFactory>
{
    private const string SigningKey = "test-only-jwt-signing-key-for-ci-2026-keep-out-of-production";
    private readonly HttpClient _client;

    public ProfileAuthorizationTests(TestApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task InternalRouteWithoutTokenReturns401()
    {
        using var response = await _client.GetAsync("/internal/user-profiles/" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("member", "profile.internal.read")]
    [InlineData("internal-service", "profile.read")]
    public async Task InternalRouteRequiresRoleAndScope(string role, string scope)
    {
        using var response = await SendAsAsync("/internal/user-profiles/" + Guid.NewGuid(), role, scope);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InternalServiceTokenReachesProfileHandler()
    {
        using var response = await SendAsAsync("/internal/user-profiles/" + Guid.NewGuid(),
            "internal-service", "profile.internal.read");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MemberCannotReadAnotherProfile()
    {
        using var response = await SendAsAsync("/api/user-profiles/" + Guid.NewGuid(),
            "member", "profile.read");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsAsync(string path, string role, string scope)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role, scope));
        return await _client.SendAsync(request);
    }

    private static string CreateToken(string role, string scope)
    {
        var jwt = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
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
