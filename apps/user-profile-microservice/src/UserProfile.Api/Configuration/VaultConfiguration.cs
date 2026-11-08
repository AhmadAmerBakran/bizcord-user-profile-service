namespace UserProfile.Api.Configuration;

internal static class VaultConfiguration
{
    internal static async Task LoadAsync(ConfigurationManager configuration, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Vault:Enabled"))
            return;

        var token = Environment.GetEnvironmentVariable("VAULT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Set VAULT_TOKEN to a scoped application token before starting the service.");

        var address = configuration["Vault:Address"]
            ?? Environment.GetEnvironmentVariable("VAULT_ADDR")
            ?? "http://localhost:8200";
        var path = configuration["Vault:SecretPath"] ?? "secret/data/user-profile";

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address.TrimEnd('/') + "/", UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };

        var secrets = await VaultSecretsReader.ReadAsync(client, token, path, cancellationToken);
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AuthenticationProviderKey"] = secrets.AuthenticationProviderKey,
            ["RabbitMq:ConnectionString"] = secrets.RabbitMqConnectionString
        });
    }
}
