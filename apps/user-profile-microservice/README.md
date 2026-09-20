# User Profile microservice

This folder contains the User Profile microservice for Bizcord.

The service is split into a few small projects so the HTTP layer, business logic and internal model do not depend on each other more than necessary.

## Structure

```text
src/
├── UserProfile.Api/
├── UserProfile.Application/
├── UserProfile.Contracts/
├── UserProfile.Domain/
└── UserProfile.Infrastructure/
tests/
Dockerfile
```

`UserProfile.Domain` contains the internal profile entity and value objects. `UserProfile.Contracts` contains the profile data that can be shared with other services. The application project contains the profile use cases, while the infrastructure project currently provides an in-memory repository. `UserProfile.Api` handles HTTP and dependency injection.

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

`UserProfile` is the main entity. It has its own internal profile id, the id of the user it belongs to, a display name and a bio. Changes to the display name and bio go through methods on the entity instead of public setters.

`DisplayName` and `Bio` are value objects. They are immutable and the domain project does not depend on ASP.NET Core, RabbitMQ or a database library.

## Shared model

`UserProfileDto` contains the user id, display name and bio. The internal profile id, value-object types and domain behavior are not part of the contract.

## Run locally

From this folder:

```bash
cd src/UserProfile.Api
dotnet restore
dotnet run
```

Swagger is available at `/swagger` in the Development environment. `UserProfile.Api.http` also contains sample requests for the CRUD endpoints.

## Messaging

Messaging is exposed through `IMessageClient` instead of using EasyNetQ directly throughout the application. The EasyNetQ implementation handles the RabbitMQ-specific details and is registered through dependency injection in `Program.cs`.

The default RabbitMQ connection is configured in `appsettings.json`:

```json
"RabbitMq": {
  "ConnectionString": "host=localhost"
}
```

The value can be overridden through configuration, for example with the environment variable `RabbitMq__ConnectionString`.
