using System.Reflection;

namespace UserProfile.Api.Messaging;

internal sealed class MessageHandlerBackgroundService : BackgroundService
{
    private static readonly MethodInfo SubscribeMethod = typeof(MessageHandlerBackgroundService)
        .GetMethod(nameof(SubscribeTypedAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly IMessageClient _messageClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyCollection<MessageHandlerRegistration> _registrations;
    private readonly ILogger<MessageHandlerBackgroundService> _logger;
    private readonly List<IDisposable> _subscriptions = new();

    public MessageHandlerBackgroundService(
        IMessageClient messageClient,
        IServiceScopeFactory scopeFactory,
        IEnumerable<MessageHandlerRegistration> registrations,
        ILogger<MessageHandlerBackgroundService> logger)
    {
        _messageClient = messageClient;
        _scopeFactory = scopeFactory;
        _registrations = registrations.ToArray();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (_registrations.Count == 0)
            {
                _logger.LogInformation("No message handlers were discovered.");
            }

            foreach (var registration in _registrations)
            {
                var subscription = await SubscribeAsync(registration, stoppingToken);
                _subscriptions.Add(subscription);

                _logger.LogInformation(
                    "Subscribed to {MessageType} with subscription id {SubscriptionId}.",
                    registration.MessageType.Name,
                    registration.SubscriptionId);
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }

    private async Task<IDisposable> SubscribeAsync(
        MessageHandlerRegistration registration,
        CancellationToken cancellationToken)
    {
        var method = SubscribeMethod.MakeGenericMethod(registration.MessageType);
        var task = (Task<IDisposable>)method.Invoke(
            this,
            new object[] { registration.SubscriptionId, cancellationToken })!;

        return await task;
    }

    private Task<IDisposable> SubscribeTypedAsync<TMessage>(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        return _messageClient.SubscribeAsync<TMessage>(
            subscriptionId,
            message => DispatchAsync(message, cancellationToken),
            cancellationToken);
    }

    private async Task DispatchAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IMessageHandler<TMessage>>();

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(message, cancellationToken);
        }
    }
}
