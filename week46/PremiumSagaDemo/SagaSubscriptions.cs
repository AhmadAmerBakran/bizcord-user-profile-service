using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;

namespace PremiumSagaDemo;

public abstract class SagaSubscriptions : BackgroundService
{
    private readonly List<IDisposable> _subscriptions = new();
    private readonly ILogger _logger;
    protected readonly IMessageClient Client;

    protected SagaSubscriptions(IMessageClient client, ILogger logger)
    {
        Client = client;
        _logger = logger;
    }

    protected abstract Task RegisterAsync(CancellationToken token);

    protected async Task SubscribeAsync<T>(
        string name,
        Func<T, Task> handler,
        CancellationToken token)
    {
        var attempts = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var subscription = await Client.SubscribeAsync(name, handler, token);
                _subscriptions.Add(subscription);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                attempts++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempts - 1, 5))));
                _logger.LogWarning(ex, "Could not subscribe to {Name}; retry in {Delay}", name, delay);
                await Task.Delay(delay, token);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RegisterAsync(stoppingToken);
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
}

public sealed class CoordinatorSubscriptions : SagaSubscriptions
{
    private readonly PremiumSagaCoordinator _coordinator;

    public CoordinatorSubscriptions(
        PremiumSagaCoordinator coordinator,
        IMessageClient client,
        ILogger<CoordinatorSubscriptions> logger) : base(client, logger)
    {
        _coordinator = coordinator;
    }

    protected override async Task RegisterAsync(CancellationToken token)
    {
        await Register<StartPremiumPurchase>(token);
        await Register<PremiumCharged>(token);
        await Register<PremiumChargeFailed>(token);
        await Register<PremiumActivated>(token);
        await Register<PremiumActivationFailed>(token);
        await Register<PremiumBadgeApplied>(token);
        await Register<PremiumBadgeFailed>(token);
        await Register<PremiumWelcomeSent>(token);
        await Register<PremiumWelcomeFailed>(token);
        await Register<PremiumBadgeRemoved>(token);
        await Register<PremiumBadgeRemovalFailed>(token);
        await Register<PremiumDeactivated>(token);
        await Register<PremiumDeactivationFailed>(token);
        await Register<PremiumRefunded>(token);
        await Register<PremiumRefundFailed>(token);
        await Register<PremiumCorrectionSent>(token);
        await Register<PremiumCorrectionFailed>(token);
        await Register<PremiumChargeReversed>(token);
    }

    private Task Register<T>(CancellationToken token) =>
        SubscribeAsync<T>(
            "week46-coordinator-" + typeof(T).Name,
            msg => _coordinator.HandleAsync(msg!, token),
            token);
}

public sealed class ParticipantSubscriptions : SagaSubscriptions
{
    private readonly ParticipantProcessor _processor;
    private readonly string _role;

    public ParticipantSubscriptions(
        ParticipantProcessor processor,
        IMessageClient client,
        IConfiguration configuration,
        ILogger<ParticipantSubscriptions> logger) : base(client, logger)
    {
        _processor = processor;
        _role = configuration["Saga:Role"]?.ToLowerInvariant()
            ?? throw new InvalidOperationException("Saga:Role is required.");
    }

    protected override async Task RegisterAsync(CancellationToken token)
    {
        switch (_role)
        {
            case "payment":
                await Register<ChargePremium>(token);
                await Register<RefundPremium>(token);
                await Register<ReportPremiumChargeReversal>(token);
                break;
            case "subscription":
                await Register<ActivatePremium>(token);
                await Register<DeactivatePremium>(token);
                break;
            case "notification":
                await Register<SendPremiumWelcome>(token);
                await Register<SendPremiumCorrection>(token);
                break;
            default:
                throw new InvalidOperationException("Unsupported saga participant: " + _role);
        }
    }

    private Task Register<T>(CancellationToken token) =>
        SubscribeAsync<T>(
            "week46-" + _role + "-" + typeof(T).Name,
            msg => _processor.HandleAsync(msg!, token),
            token);
}

public sealed class SagaRecoveryService : BackgroundService
{
    private readonly JsonStateStore<PremiumSagaState> _store;
    private readonly IMessageClient _client;
    private readonly ILogger<SagaRecoveryService> _logger;

    public SagaRecoveryService(
        JsonStateStore<PremiumSagaState> store,
        IMessageClient client,
        ILogger<SagaRecoveryService> logger)
    {
        _store = store;
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
                var sagas = await _store.AllAsync(stoppingToken);
                foreach (var saga in sagas)
                {
                    var pending = saga.PendingCommand();
                    if (pending is null)
                    {
                        continue;
                    }

                    try
                    {
                        await PremiumSagaPublisher.PublishAsync(_client, pending, stoppingToken);
                    }
                    catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Could not resume saga {SagaId}", saga.SagaId);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
