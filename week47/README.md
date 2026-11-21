# Week 47: Fluentd and GraphQL

This week adds two presentation and infrastructure features to the existing User Profile service. Neither one changes where profiles are stored or how the domain behaves.

## Task 01: Fluentd sidecar

The API writes logs to its standard output using the built in .NET console logger. The Compose stack starts a Fluentd container alongside the API and RabbitMQ. Docker's Fluentd logging driver picks up the API container's stdout and stderr and sends them over the forward protocol to the local Fluentd endpoint on port 24224. The collector prints the events to its own console, standing in for the central destination described in the assignment.

I used the Docker logging driver instead of mounting Docker's internal container log directory. The latter assumes the host exposes `/var/lib/docker/containers`, which is not a dependable assumption on Docker Desktop. In this setup Fluentd does not need access to the Docker socket or host filesystem. The logging configuration is in `apps/user-profile-microservice/fluentd/fluent.conf`.

Start the stack from the repository root after setting a local signing key of at least 32 UTF-8 bytes. Do not commit that key.

```bash
export AUTHENTICATION_PROVIDER_KEY="$(openssl rand -hex 32)"
docker compose -f apps/user-profile-microservice/docker-compose.yml up --build -d
docker compose -f apps/user-profile-microservice/docker-compose.yml logs -f fluentd
```

In PowerShell, set `$env:AUTHENTICATION_PROVIDER_KEY` to a sufficiently long secret before running `docker compose`. The stack uses the same `RabbitMq__ConnectionString` as before. An unset signing key intentionally prevents the API from starting; it is not replaced with a demonstration secret.

Fluentd should print an event containing `User Profile API logging is handled by stdout.` when the API starts. A request to `http://localhost:8080/api/user-profiles` without a token should return 401. The application still logs normally when run outside Compose; the collector is a deployment concern rather than an application dependency.

```bash
docker compose -f apps/user-profile-microservice/docker-compose.yml down
```

The assignment's external log destination is optional. I kept the output on the Fluentd console, as specified for the basic task. This is not durable log storage. The Compose configuration also assumes the Docker daemon can reach the host's published `127.0.0.1:24224` port. For a remote Docker daemon, that address must be changed to one reachable from the daemon.

## Task 02: GraphQL presentation

HotChocolate provides a GraphQL endpoint at `POST /graphql` alongside the existing REST controllers. Its resolvers call `IUserProfileService`, the same application service that handles the REST requests. There is no separate profile repository, duplicate business logic or new domain entity.

The schema provides three queries:

| Query | Who can use it | Purpose |
| --- | --- | --- |
| `myProfile` | Member or admin with `profile.read` | Read the subject's own profile |
| `userProfile(userId: UUID!)` | Owner or admin with `profile.read` | Read a profile by user id |
| `userProfiles` | Admin with `profile.read` | Read all profiles |

The fields available for selection are `userId`, `displayName`, `bio` and `isPremium`. The GraphQL model comes from the existing public profile DTO, not the domain entity. Frontend clients can request only the fields they need. For example, with a valid bearer token:

```bash
curl http://localhost:8080/graphql \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"query":"query { myProfile { displayName isPremium } }"}'
```

The `ProfileRead` policy protects the endpoint, the GraphQL fields have their own role and scope policies, and the profile lookup checks ownership. These checks matter: adding a new presentation endpoint should not silently undo the access restrictions introduced in Week 45. We left profile creation, updates and deletion on the existing REST API; the assignment requires a query interface, not a second set of write operations.

GraphQL is a BFF-style presentation layer in the existing API process, not a fully independent frontend service. An Anti-Corruption Layer was part of the reading material, but neither assignment asks for an integration with a legacy system, so none was added.

## Verification

The GraphQL integration tests cover anonymous requests, missing scopes, selective field retrieval, profile ownership and administrator queries. The existing unit, contract, integration and saga suites still run in GitHub Actions. A separate sidecar job builds the Compose stack and checks that an API startup message reaches Fluentd. The main CI job validates the Compose configuration.

To run GraphQL integration tests locally, start RabbitMQ and then run:

```bash
docker compose -f apps/user-profile-microservice/docker-compose.yml up -d rabbitmq
dotnet test apps/user-profile-microservice/tests/UserProfile.IntegrationTests/UserProfile.IntegrationTests.csproj --configuration Release
```

The test factory supplies a test-only signing key and uses the local RabbitMQ broker. It is not a production secret. A green build and successful sidecar smoke test are needed before merging this branch.
