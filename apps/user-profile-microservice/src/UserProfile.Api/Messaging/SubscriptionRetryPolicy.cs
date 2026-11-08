namespace UserProfile.Api.Messaging;

internal sealed class SubscriptionRetryPolicy
{
    private readonly Func<TimeSpan, CancellationToken, Task> _wait;

    public SubscriptionRetryPolicy(Func<TimeSpan, CancellationToken, Task>? wait = null)
    {
        _wait = wait ?? Task.Delay;
    }

    public async Task<IDisposable> ExecuteAsync(
        Func<CancellationToken, Task<IDisposable>> subscribe,
        Action<Exception, TimeSpan, int> onRetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscribe);
        ArgumentNullException.ThrowIfNull(onRetry);

        var retries = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await subscribe(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                retries++;
                var seconds = Math.Min(30, Math.Pow(2, Math.Min(retries - 1, 5)));
                var delay = TimeSpan.FromSeconds(seconds);

                onRetry(exception, delay, retries);
                await _wait(delay, cancellationToken);
            }
        }
    }
}
