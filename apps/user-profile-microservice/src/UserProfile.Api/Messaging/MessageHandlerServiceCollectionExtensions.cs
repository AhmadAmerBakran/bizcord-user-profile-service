using System.Reflection;

namespace UserProfile.Api.Messaging;

public static class MessageHandlerServiceCollectionExtensions
{
    public static IServiceCollection AddMessageHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
        {
            throw new ArgumentException("At least one assembly must be provided.", nameof(assemblies));
        }

        var handlerInterface = typeof(IMessageHandler<>);

        var discoveredHandlers = assemblies
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters)
            .SelectMany(type => type.ImplementedInterfaces
                .Where(@interface =>
                    @interface.IsGenericType &&
                    @interface.GetGenericTypeDefinition() == handlerInterface)
                .Select(@interface => new HandlerDescriptor(
                    @interface,
                    type.AsType(),
                    @interface.GenericTypeArguments[0])))
            .GroupBy(descriptor => new
            {
                descriptor.ServiceType,
                descriptor.ImplementationType
            })
            .Select(group => group.First())
            .ToArray();

        foreach (var handler in discoveredHandlers)
        {
            services.AddScoped(handler.ServiceType, handler.ImplementationType);
        }

        foreach (var messageType in discoveredHandlers
                     .Select(handler => handler.MessageType)
                     .Distinct())
        {
            services.AddSingleton(new MessageHandlerRegistration(
                messageType,
                CreateSubscriptionId(messageType)));
        }

        services.AddHostedService<MessageHandlerBackgroundService>();

        return services;
    }

    private static string CreateSubscriptionId(Type messageType)
    {
        var messageName = messageType.FullName ?? messageType.Name;
        var safeMessageName = messageName
            .Replace('.', '-')
            .Replace('+', '-');

        return $"user-profile-{safeMessageName}";
    }

    private sealed record HandlerDescriptor(
        Type ServiceType,
        Type ImplementationType,
        Type MessageType);
}
