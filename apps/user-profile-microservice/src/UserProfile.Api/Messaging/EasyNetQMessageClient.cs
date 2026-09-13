using EasyNetQ;

namespace UserProfile.Api.Messaging;

public sealed class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;

    public EasyNetQMessageClient(IBus bus)
    {
        _bus = bus;
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        return _bus.PubSub.PublishAsync(message, cancellationToken);
    }

    public async Task<IDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationToken);

        return subscription;
    }
}
