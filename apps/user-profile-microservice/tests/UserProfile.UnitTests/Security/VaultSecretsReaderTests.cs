using System.Net;
using System.Text;
using UserProfile.Api.Configuration;
using Xunit;

namespace UserProfile.UnitTests.Security;

public sealed class VaultSecretsReaderTests
{
    private const string Path = "secret/data/user-profile";

    [Fact]
    public async Task LoadsKvV2SecretUsingApplicationToken()
    {
        using var client = Client(request =>
        {
            Assert.Equal("/v1/secret/data/user-profile", request.RequestUri!.AbsolutePath);
            Assert.Equal("scoped-token", request.Headers.GetValues("X-Vault-Token").Single());
            return Reply(HttpStatusCode.OK,
                """{"data":{"data":{"AuthenticationProviderKey":"example-signing-key-longer-than-32-bytes","RabbitMqConnectionString":"host=rabbitmq;username=demo;password=demo"}}}""");
        });
        var value = await VaultSecretsReader.ReadAsync(client, "scoped-token", Path);
        Assert.Equal("example-signing-key-longer-than-32-bytes", value.AuthenticationProviderKey);
        Assert.Equal("host=rabbitmq;username=demo;password=demo", value.RabbitMqConnectionString);
    }

    [Fact]
    public async Task RejectsInvalidToken()
    {
        using var client = Client(_ => Reply(HttpStatusCode.Forbidden, "{}"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => VaultSecretsReader.ReadAsync(client, "bad-token", Path));
        Assert.Contains("denied access", error.Message);
    }

    [Fact]
    public async Task RejectsIncompleteSecret()
    {
        using var client = Client(_ => Reply(HttpStatusCode.OK,
            """{"data":{"data":{"AuthenticationProviderKey":"example"}}}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VaultSecretsReader.ReadAsync(client, "token", Path));
    }

    [Fact]
    public async Task RejectsInvalidSecretPath()
    {
        using var client = Client(_ => throw new InvalidOperationException("Request must not be sent"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => VaultSecretsReader.ReadAsync(client, "token", "../another-path"));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
        new(new StubHandler(reply)) { BaseAddress = new Uri("http://vault:8200/") };

    private static HttpResponseMessage Reply(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _reply;
        internal StubHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) => _reply = reply;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_reply(request));
    }
}
