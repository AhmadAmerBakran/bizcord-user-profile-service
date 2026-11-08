using System.Net;
using System.Text.Json;

namespace UserProfile.Api.Configuration;

internal sealed record VaultSecrets(string AuthenticationProviderKey, string RabbitMqConnectionString);

internal static class VaultSecretsReader
{
    internal static async Task<VaultSecrets> ReadAsync(
        HttpClient client,
        string token,
        string secretPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("VAULT_TOKEN is required when Vault is enabled.");

        if (string.IsNullOrWhiteSpace(secretPath) ||
            secretPath.StartsWith('/') ||
            secretPath.Contains("..", StringComparison.Ordinal) ||
            secretPath.Contains('?', StringComparison.Ordinal) ||
            secretPath.Contains('#', StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid Vault secret path.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/" + secretPath);
        request.Headers.Add("X-Vault-Token", token);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Vault denied access. Check the application token, policy and TTL.");

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("data", out var values) ||
            !values.TryGetProperty("AuthenticationProviderKey", out var keyValue) ||
            !values.TryGetProperty("RabbitMqConnectionString", out var rabbitValue))
            throw new InvalidOperationException("Vault secret is missing required fields.");

        var key = keyValue.GetString();
        var connection = rabbitValue.GetString();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Vault secret contains empty required fields.");

        return new VaultSecrets(key, connection);
    }
}
