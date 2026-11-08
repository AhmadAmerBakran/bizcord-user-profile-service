# Week 44: failure review

Reviewed against the User Profile service on `master` before making the week 44 changes. This is the implementation in this repository, not the whole Bizcord architecture.

## RabbitMQ subscriptions

`MessageHandlerBackgroundService` subscribes to `MessagePostedEvent` when the API starts. A failed subscription currently throws out of the background service. There is no application-level retry, so a temporary broker outage can leave the consumer unavailable. The Compose health check helps with initial startup but cannot prevent later broker outages.

Priority: high. Retry subscription setup with backoff and cancellation support. Log unsuccessful attempts rather than silently dropping the consumer.

## Event processing and publishing

`MessagePostedHandler` records the author's message activity before publishing `UserProfileActivityUpdatedEvent`. If publishing fails, the local count has already changed. A replay of the same `MessagePostedEvent` will increase the count again because the repository does not track processed message IDs.

Priority: high. Make activity updates idempotent by `MessageId`. A repeated delivery should leave the count unchanged and may republish the result. Downstream consumers must also handle repeated result events.

Publishing itself can still fail. There is no durable outbox to guarantee delivery after a process crash.

## In-memory storage

`InMemoryUserProfileRepository` and `InMemoryUserMessageActivityRepository` store state inside the API process. Restarting the container loses profiles, activity counts and any record of processed messages. Running multiple API replicas would also create separate, inconsistent copies of this state.

Priority: high for a deployed system. The proper fix is persistent storage with a durable processed-message record. Retry and fallback policies cannot make in-memory data durable.

## Timeouts and other dependencies

The RabbitMQ adapter forwards cancellation tokens, but the service does not configure an explicit per-operation timeout for publishing or subscribing. A slow operation could remain pending longer than callers expect. A bounded operation budget should be considered alongside the broker client's own timeout and recovery settings.

The current User Profile service makes no outbound HTTP requests to the other Bizcord services. Its REST endpoints use local repositories, not a remote database or payment provider. Introducing HTTP retry or a database fallback here would not address a failure point in the current code.

## This week's implementation

Subscription retry is implemented in the API messaging layer, alongside the background consumer. Broker connection management does not belong in the domain model. Message deduplication is implemented in the activity repository, where the processed ID and message count can be updated under the same lock.

The subscription retry uses exponential backoff capped at 30 seconds and stops on host shutdown. It is intended for subscription setup failures; it does not replace the broker library's connection recovery or add an operation timeout. The retry currently logs and repeats all subscription exceptions, so persistent configuration errors also require attention in the logs.

The processed-message record prevents duplicate count updates within one running process. A repeated input can still lead to a repeated output event. We have not added a fallback that invents profile data or silently discards events.

## Verification

The new unit tests simulate failed subscription attempts, cancellation before and during retry, a publish failure followed by replay, duplicate message IDs and concurrent repeated deliveries. They run without RabbitMQ:

```bash
dotnet test tests/UserProfile.UnitTests/UserProfile.UnitTests.csproj --configuration Release
```

The existing contract and broker-backed integration tests run in CI along with the Docker build and startup check. A durable outbox, persistent data, explicit publish timeouts and broker failure recovery after a full process crash remain follow-up work.
