using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace UserProfile.IntegrationTests.Infrastructure;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var connectionString = Environment.GetEnvironmentVariable("BIZCORD_TEST_RABBITMQ")
                ?? "host=localhost;username=bizcord;password=bizcord";

            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:ConnectionString"] = connectionString
            });
        });
    }
}
