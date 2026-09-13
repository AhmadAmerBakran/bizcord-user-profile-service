using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UserProfile.Api.Messaging;

public static class MessageClientServiceCollectionExtensions
{
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["RabbitMq:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "RabbitMQ connection string is missing from configuration.");
        }

        services.AddSingleton<IBus>(_ => RabbitHutch.CreateBus(connectionString));
        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}
