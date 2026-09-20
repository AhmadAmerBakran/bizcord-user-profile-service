# User Profile microservice

This folder contains the User Profile microservice for Bizcord.

The service is built with ASP.NET Core. The API project contains the HTTP and messaging setup, while the domain project contains the profile model and is kept independent of frameworks and infrastructure code.

## Structure

```text
src/
├── UserProfile.Api/
└── UserProfile.Domain/
tests/
Dockerfile
```

`UserProfile.Api` is the executable Web API. `UserProfile.Domain` contains the domain model used by the service.

## Domain model

The current model is intentionally small and focuses on the parts owned by this microservice.

`UserProfile` is the main entity. It has its own profile id, the id of the user it belongs to, a display name and a bio. Changes to the display name and bio go through methods on the entity instead of exposing public setters.

`DisplayName` and `Bio` are value objects. They have no identity of their own and are immutable once created. The domain project does not depend on ASP.NET Core, RabbitMQ or a database library, so the model can change without being tied to those implementation details.

The model will be extended as the remaining service requirements are implemented.

## Run locally

From this folder:

```bash
cd src/UserProfile.Api
dotnet restore
dotnet run
```

When the application runs in the Development environment, Swagger is available at `/swagger`.

## Messaging

Messaging is exposed through `IMessageClient` instead of using EasyNetQ directly throughout the application. The EasyNetQ implementation handles the RabbitMQ-specific details and is registered through dependency injection in `Program.cs`.

The client currently supports publishing messages and creating subscriptions. A subscription returns `IDisposable`, which can be disposed when the consumer should stop receiving messages.

The default RabbitMQ connection is configured in `appsettings.json`:

```json
"RabbitMq": {
  "ConnectionString": "host=localhost"
}
```

The value can be overridden through configuration, for example with the environment variable `RabbitMq__ConnectionString`.

The sample `WeatherForecast` endpoint is still temporary boilerplate and will be replaced as the User Profile API is implemented.
