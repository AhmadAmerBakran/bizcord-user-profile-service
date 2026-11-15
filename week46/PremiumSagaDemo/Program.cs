using PremiumSagaDemo;
using Shared.Contracts.Sagas;
using UserProfile.Api.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMessageClient(builder.Configuration);

var role = builder.Configuration["Saga:Role"]?.ToLowerInvariant() ?? "coordinator";
var stateFile = builder.Configuration["Saga:StateFile"]
    ?? Path.Combine(AppContext.BaseDirectory, role + ".json");

switch (role)
{
    case "coordinator":
        builder.Services.AddSingleton(new JsonStateStore<PremiumSagaState>(stateFile));
        builder.Services.AddSingleton<PremiumSagaCoordinator>();
        builder.Services.AddHostedService<CoordinatorSubscriptions>();
        builder.Services.AddHostedService<SagaRecoveryService>();
        break;
    case "payment":
    case "subscription":
    case "notification":
        builder.Services.AddSingleton(new JsonStateStore<ParticipantState>(stateFile));
        builder.Services.AddSingleton<ParticipantProcessor>(sp => new ParticipantProcessor(
            role,
            builder.Configuration["Saga:FailStep"],
            sp.GetRequiredService<JsonStateStore<ParticipantState>>(),
            sp.GetRequiredService<IMessageClient>()));
        builder.Services.AddHostedService<ParticipantSubscriptions>();
        break;
    default:
        throw new InvalidOperationException("Unknown saga role: " + role);
}

var app = builder.Build();

// Demo controls are deliberately unavailable outside Development.
// Production systems must authenticate purchase requests and provider callbacks.
if (role == "coordinator" && app.Environment.IsDevelopment())
{
    app.MapPost("/sagas/premium", async (
        PurchaseRequest request,
        PremiumSagaCoordinator coordinator,
        CancellationToken token) =>
    {
        if (request.UserId == Guid.Empty || request.Amount <= 0)
        {
            return Results.BadRequest("A user ID and a positive amount are required.");
        }

        var sagaId = Guid.NewGuid();
        await coordinator.HandleAsync(
            new StartPremiumPurchase(sagaId, request.UserId, request.Amount), token);
        return Results.Accepted("/sagas/premium/" + sagaId, new { sagaId });
    });

    app.MapGet("/sagas/premium/{sagaId:guid}", async (
        Guid sagaId,
        PremiumSagaCoordinator coordinator,
        CancellationToken token) =>
    {
        var saga = await coordinator.GetAsync(sagaId, token);
        return saga is null ? Results.NotFound() : Results.Ok(saga);
    });

    app.MapPost("/sagas/premium/{sagaId:guid}/payment-reversal", async (
        Guid sagaId,
        PaymentReversalRequest request,
        PremiumSagaCoordinator coordinator,
        IMessageClient messageClient,
        CancellationToken token) =>
    {
        var saga = await coordinator.GetAsync(sagaId, token);
        if (saga is null)
        {
            return Results.NotFound();
        }

        if (saga.Phase != PremiumSagaPhase.Completed)
        {
            return Results.Conflict("Only completed purchases can receive a simulated late reversal.");
        }

        await messageClient.PublishAsync(
            new ReportPremiumChargeReversal(sagaId, saga.UserId,
                string.IsNullOrWhiteSpace(request.Reason) ? "Provider reversed the charge" : request.Reason),
            token);
        return Results.Accepted();
    });
}

app.Run();

public sealed record PurchaseRequest(Guid UserId, decimal Amount);
public sealed record PaymentReversalRequest(string? Reason);
