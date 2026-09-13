namespace UserProfile.Api.Messaging;

public interface IMessageClient
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default);

    Task<IDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default);
}
