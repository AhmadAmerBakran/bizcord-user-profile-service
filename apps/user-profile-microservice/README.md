# User Profile microservice

This folder contains the User Profile microservice for Bizcord.

The service is split into a few small projects so the HTTP layer, business logic and internal model do not depend on each other more than necessary.

## Structure

```text
packages/
└── Shared.Contracts/
src/
├── UserProfile.Api/
├── UserProfile.Application/
├── UserProfile.Contracts/
├── UserProfile.Domain/
└── UserProfile.Infrastructure/
tests/
├── UserProfile.UnitTests/
├── UserProfile.ContractTests/
└── UserProfile.IntegrationTests/
Dockerfile
docker-compose.yml
```

`UserProfile.Domain` contains the internal profile entity, message activity state and value objects. `UserProfile.Contracts` contains the profile data used by the REST API. The application project contains the profile use cases, while the infrastructure project currently provides in memory repositories. `UserProfile.Api` handles HTTP, RabbitMQ and dependency injection. The message contracts shared with the other Bizcord services are kept in `packages/Shared.Contracts`.

## REST API

The API exposes the basic CRUD operations for user profiles:

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/user-profiles` | Create a profile |
| `GET` | `/api/user-profiles` | List profiles |
| `GET` | `/api/user-profiles/{userId}` | Get one profile |
| `PUT` | `/api/user-profiles/{userId}` | Update a profile |
| `DELETE` | `/api/user-profiles/{userId}` | Delete a profile |

The API returns the shared `UserProfileDto` instead of exposing the internal domain entity. A duplicate profile returns `409 Conflict`, invalid input returns `400 Bad Request`, and missing profiles return `404 Not Found`.

Profiles are stored in memory for now, so restarting the application clears the data.

## Domain model

`UserProfile` is the main profile entity. It has its own internal profile id, the id of the user it belongs to, a display name and a bio. Changes to the display name and bio go through methods on the entity instead of public setters.

`UserMessageActivity` keeps a small activity summary for each author seen in `MessagePostedEvent`. It records how many posted messages have been processed and the most recent message timestamp. The state is kept separate from the profile itself because a message can arrive before a profile has been created locally.

`DisplayName` and `Bio` are value objects. They are immutable and the domain project does not depend on ASP.NET Core, RabbitMQ or a database library.

## Shared model

`UserProfileDto` contains the user id, display name and bio. The internal profile id, value object types and domain behavior are not part of the REST contract.

`MessagePostedEvent` is the incoming system contract for a posted message. After handling it, this service publishes `UserProfileActivityUpdatedEvent`. The result includes the original `MessageId`, which makes the message flow traceable across services.

## Run locally

From this folder:

```bash
cd src/UserProfile.Api
dotnet restore
dotnet run
```

Swagger is available at `/swagger` in the Development environment. `UserProfile.Api.http` also contains sample requests for the CRUD endpoints.

## Docker

The Dockerfile uses .NET 8 and builds the service in stages. The SDK image is only used for restore, build and publish. The final image contains the ASP.NET runtime and the published application.

Build the image from this folder:

```bash
docker build -t bizcord-user-profile .
```

Run it on port 8080:

```bash
docker run --rm -p 8080:8080 bizcord-user-profile
```

For normal development it is easier to start the API together with RabbitMQ using Docker Compose:

```bash
docker compose up --build
```

The API is then available on port `8080`. RabbitMQ uses port `5672`, and its management page is available on port `15672` with the development login `bizcord` / `bizcord`.

The Compose file waits until RabbitMQ is running and listening on its AMQP port before starting the API. Inside the Compose network the RabbitMQ service is reached by the name `rabbitmq`, and the API gets its connection string through `RabbitMq__ConnectionString`.

Stop the containers with:

```bash
docker compose down
```

## Messaging

Messaging is exposed through `IMessageClient` instead of using EasyNetQ directly throughout the application. The EasyNetQ implementation handles the RabbitMQ specific details and is registered through dependency injection in `Program.cs`.

Message handlers implement `IMessageHandler<TMessage>`. At startup the API scans its assembly, registers the handlers it finds and starts their subscriptions from a background service. `MessagePostedHandler` consumes `MessagePostedEvent`, updates the author's message activity summary and publishes `UserProfileActivityUpdatedEvent`.

The default RabbitMQ connection is configured in `appsettings.json`:

```json
"RabbitMq": {
  "ConnectionString": "host=localhost"
}
```

The value can be overridden through configuration, for example with the environment variable `RabbitMq__ConnectionString`.

## Testing

The message posting flow is covered at three scopes. The unit test checks the handler's own logic and the event it publishes. The consumer contract test checks that the handler can process the minimum valid shared message and still publish its result. The integration test uses the real EasyNetQ client, the real background handler and a real RabbitMQ broker.

The unit and contract tests do not need RabbitMQ:

```bash
dotnet test tests/UserProfile.UnitTests/UserProfile.UnitTests.csproj
dotnet test tests/UserProfile.ContractTests/UserProfile.ContractTests.csproj
```

For the integration test, start RabbitMQ from this folder first and then run the test project:

```bash
docker compose up -d rabbitmq
dotnet test tests/UserProfile.IntegrationTests/UserProfile.IntegrationTests.csproj
```

The integration test uses `host=localhost;username=bizcord;password=bizcord` by default. A different test broker can be selected with the `BIZCORD_TEST_RABBITMQ` environment variable.
