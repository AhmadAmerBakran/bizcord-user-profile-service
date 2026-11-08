using FluentAssertions;
using UserProfile.Api.Messaging;
using Xunit;

namespace UserProfile.UnitTests.Messaging;

public sealed class SubscriptionRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientSubscriptionFailuresWithBackoff()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var retries = new List<int>();
        var subscription = new TestSubscription();
        var policy = new SubscriptionRetryPolicy((delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return attempts <= 3
                    ? Task.FromException<IDisposable>(new IOException("Broker unavailable"))
                    : Task.FromResult<IDisposable>(subscription);
            },
            (_, _, retry) => retries.Add(retry),
            CancellationToken.None);

        result.Should().BeSameAs(subscription);
        attempts.Should().Be(4);
        delays.Should().Equal(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4));
        retries.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSubscribeAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempts = 0;
        var policy = new SubscriptionRetryPolicy();

        Func<Task> act = async () =>
        {
            await policy.ExecuteAsync(
                _ =>
                {
                    attempts++;
                    return Task.FromResult<IDisposable>(new TestSubscription());
                },
                (_, _, _) => { },
                cancellation.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        attempts.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_StopsRetryingWhenCancelledDuringBackoff()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        var policy = new SubscriptionRetryPolicy((_, _) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        });

        Func<Task> act = async () =>
        {
            await policy.ExecuteAsync(
                _ =>
                {
                    attempts++;
                    return Task.FromException<IDisposable>(new IOException("Broker unavailable"));
                },
                (_, _, _) => { },
                cancellation.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        attempts.Should().Be(1);
    }

    private sealed class TestSubscription : IDisposable
    {
        public void Dispose() { }
    }
}
