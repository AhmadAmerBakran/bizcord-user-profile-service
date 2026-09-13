# User Profile microservice

This folder contains the User Profile microservice for Bizcord.

The service is built with ASP.NET Core. The current version includes the initial Web API boilerplate and a small messaging abstraction for publishing and subscribing to messages through RabbitMQ.

## Structure

```text
src/        Application source code
tests/      Automated tests
Dockerfile  Container definition
```

The API project is located in `src/UserProfile.Api`.

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
