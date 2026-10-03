using UserProfile.Api.Messaging;

namespace UserProfile.IntegrationTests.Infrastructure;

public sealed class MessageCapture<TMessage> : IAsyncDisposable
{
    private readonly TaskCompletionSource<TMessage> _message =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly IDisposable _subscription;

    private MessageCapture(IDisposable subscription)
    {
        _subscription = subscription;
    }

    public static async Task<MessageCapture<TMessage>> StartAsync(
        IMessageClient client,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        MessageCapture<TMessage>? capture = null;

        var subscription = await client.SubscribeAsync<TMessage>(
            subscriptionId,
            message =>
            {
                capture!._message.TrySetResult(message);
                return Task.CompletedTask;
            },
            cancellationToken);

        capture = new MessageCapture<TMessage>(subscription);
        return capture;
    }

    public Task<TMessage> WaitForMessageAsync(TimeSpan timeout)
    {
        return _message.Task.WaitAsync(timeout);
    }

    public ValueTask DisposeAsync()
    {
        _subscription.Dispose();
        return ValueTask.CompletedTask;
    }
}
