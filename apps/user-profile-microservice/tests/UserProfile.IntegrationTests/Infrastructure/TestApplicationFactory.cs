using Microsoft.AspNetCore.Mvc.Testing;

namespace UserProfile.IntegrationTests.Infrastructure;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    public TestApplicationFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("BIZCORD_TEST_RABBITMQ")
            ?? Environment.GetEnvironmentVariable("RabbitMq__ConnectionString")
            ?? "host=localhost;username=bizcord;password=bizcord";

        Environment.SetEnvironmentVariable("RabbitMq__ConnectionString", connectionString);
        Environment.SetEnvironmentVariable("AuthenticationProviderKey", "test-only-jwt-signing-key-for-ci-2026-keep-out-of-production");
    }
}
