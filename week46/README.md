# Week 46: Premium purchase saga

## What this exercise covers

Bizcord currently has a working User Profile service. Payment, subscription management and notifications are not implemented in this repository as production services. For this exercise I have added a small application that can run three independent message consumers, alongside a coordinator and the existing profile API. The extra consumers use saved local state and simulate their business operations. No money is charged and no email is sent.

The scenario comes from Task 01. A member buys a premium subscription. The payment seems to succeed, so the subscription is activated, the profile gains a premium badge and a welcome notification is recorded. The interesting failure is a settlement reversal that arrives after the user has already started using premium features.

I chose orchestration for Task 02. A single coordinator owns the order of the work, the saved progress and the compensation decisions. With choreography, payment, subscription, profile and notification would each need enough knowledge of the other services' events to handle the delayed reversal. That would make this particular flow harder to follow and debug. Orchestration introduces a coordinator dependency, but gives the purchase a visible state machine.

## Workflow before implementation

The purchase begins with `StartPremiumPurchase`, which contains a new `SagaId`, a `UserId` and the price. The same two identifiers are present in every command and outcome.

**Payment.** The coordinator sends `ChargePremium`. The payment consumer replies with `PremiumCharged` or `PremiumChargeFailed`. If a later step fails after a successful charge, the coordinator sends `RefundPremium` and waits for `PremiumRefunded`. A rejected charge does not need a refund.

**Subscription.** A successful payment starts `ActivatePremium`. The response is `PremiumActivated` or `PremiumActivationFailed`. When an activated subscription needs to be undone, `DeactivatePremium` is followed by `PremiumDeactivated`.

**Profile.** Activation starts `ApplyPremiumBadge`. The User Profile service changes its own domain object and replies with `PremiumBadgeApplied`. If the profile does not exist it sends `PremiumBadgeFailed`. The compensation is `RemovePremiumBadge`, which produces `PremiumBadgeRemoved`.

**Notification.** The coordinator sends `SendPremiumWelcome`, followed by `PremiumWelcomeSent` or `PremiumWelcomeFailed`. Delivery is not reversible. If the original welcome was delivered before a late failure, the coordinator sends `SendPremiumCorrection`. The notification consumer records the correction as a separate message.

A failed step causes compensation in reverse order. The coordinator only compensates operations known to have succeeded. A late `PremiumChargeReversed` from the payment consumer means the charge was already reversed by the provider, so an extra refund would be incorrect. The badge and subscription are removed and the recipient gets a correction.

If a compensation cannot be completed, the saga enters `AttentionRequired`. That state is intentional: pretending everything was rolled back would hide the inconsistency. An early reversal while another operation is still in flight also needs reconciliation, because its eventual result is unknown.

## State, delivery and service ownership

Each service processes messages through the existing `IMessageClient` and EasyNetQ adapter. The coordinator keeps its own state on disk, including the current step, completed actions, the reason for failure and a short history. Each simulated consumer has a separate state file. Files are written with atomic replacement. This is a single instance teaching example, not a replacement for a transactional database or an outbox.

The coordinator republishes an outstanding command every 20 seconds. Participants recognize repeated commands by `SagaId` and return the same outcome without repeating the simulated side effect. The profile consumer also ignores an old apply command after that saga has been compensated. A saved pending step is recoverable after a process restart.

There are still important limits. RabbitMQ publishing is not atomic with the JSON state write, multiple replicas must not write to the same file, and the profile records and badge deduplication are in memory in the original service. In a production service I would use a database with optimistic concurrency, a transactional outbox, durable participant state and monitored operator recovery. Concurrency between separate purchases for the same user would also need an explicit business rule.

Event Sourcing was another learning resource this week, but it was not required by these two assignments. These JSON snapshots represent saga progress, not an Event Sourcing implementation.

## Run the demonstration

From the repository root, run:

```bash
docker compose -f week46/compose.yaml up --build -d
```

The User Profile API is at `http://localhost:8080`, the coordinator is at `http://localhost:8081`, and RabbitMQ management is at `http://localhost:15672`. The demo's coordinator HTTP routes are available only in the Development environment. The profile API still uses the authentication requirements introduced in Week 45.

Create a user profile first with the authenticated `POST /api/user-profiles` endpoint. The same user's UUID is used to start the purchase:

```bash
curl -X POST http://localhost:8081/sagas/premium \
  -H "Content-Type: application/json" \
  -d '{"userId":"11111111-1111-4111-8111-111111111111","amount":12.00}'
```

Replace the UUID with the user whose profile you created. The response includes the saga ID. Query its state with:

```bash
curl http://localhost:8081/sagas/premium/REPLACE_WITH_SAGA_ID
```

Once the state is `Completed`, simulate the payment provider's delayed failure:

```bash
curl -X POST http://localhost:8081/sagas/premium/REPLACE_WITH_SAGA_ID/payment-reversal \
  -H "Content-Type: application/json" \
  -d '{"reason":"Settlement rejected after initial approval"}'
```

The expected final state is `Compensated`. The history records the badge removal, deactivation and correction notice. It does not include a second refund because the provider has already reversed the charge.

For another failure path, set `Saga__FailStep` on one of the simulated participants before starting a new purchase. The payment role accepts `charge` and `refund`, the subscription role accepts `activate` and `deactivate`, and the notification role accepts `welcome` and `correction`. A missing user profile provides a natural badge failure.

## Verification

Run the new saga tests:

```bash
dotnet test week46/PremiumSagaDemo.Tests/PremiumSagaDemo.Tests.csproj --configuration Release
```

Run the original profile tests to check that the existing functionality is still intact:

```bash
dotnet test apps/user-profile-microservice/tests/UserProfile.UnitTests/UserProfile.UnitTests.csproj --configuration Release
dotnet test apps/user-profile-microservice/tests/UserProfile.ContractTests/UserProfile.ContractTests.csproj --configuration Release
dotnet test apps/user-profile-microservice/tests/UserProfile.IntegrationTests/UserProfile.IntegrationTests.csproj --configuration Release
```

The last project requires RabbitMQ. GitHub Actions runs the tests with its own RabbitMQ service and also builds the demo container. When finished locally, run `docker compose -f week46/compose.yaml down`. Leave the volumes intact if you want to inspect saved saga state after a restart.
