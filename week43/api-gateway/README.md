# Week 43: User Profile gateway

This is the configuration we used for the API gateway exercises in System Integration, week 43. It belongs to our User Profile service but runs in a separate clone of the lecturer's Ocelot gateway.

We are keeping the work on the feat/week-43-api-gateway branch for reference. It is not intended to be merged into main or master at this stage. When we start integrating more services behind one gateway, we can reuse the route file and recheck it against the gateway version in use at that time.

## Where the code goes

Keep the User Profile service in the bizcord-user-profile-service repository. Clone the gateway beside it from https://github.com/patrickstolc/api-gateway. The teaching gateway uses .NET 10 and Ocelot 25.0.2; our User Profile API still uses .NET 8.

From this folder in PowerShell, install the routes into a separate gateway clone:

    .\setup-week43.ps1 -GatewayPath ..\api-gateway

The script clones the gateway when necessary, adds its own ocelot.user-profile.json, and removes the unused example route. It does not make any changes to our main project.

## Running locally

Start RabbitMQ from the User Profile application directory:

    cd <path-to-bizcord-user-profile-service>\apps\user-profile-microservice
    docker compose up -d rabbitmq

Start the User Profile API in the same directory:

    $env:RabbitMq__ConnectionString = 'host=localhost;username=bizcord;password=bizcord'
    dotnet run --project .\src\UserProfile.Api\UserProfile.Api.csproj --launch-profile http

The http launch profile uses port 5194. The gateway configuration sends requests to host.docker.internal:5194 because the gateway is running in Docker. If Docker cannot reach that listener, check the address the API is bound to and the local firewall rather than changing gateway authentication rules.

Start the gateway in a second PowerShell terminal:

    cd <path-to-api-gateway>
    docker compose up --build

Its address is http://localhost:8080, with the demonstration token faucet at POST /faucet and Swagger at http://localhost:8080/swagger. Avoid running the User Profile Compose service unchanged at the same time: its published port 8080 conflicts with the gateway.

## Routes and access rules

| Gateway route | User Profile route | Access |
| --- | --- | --- |
| GET /user-profiles/me | GET /api/user-profiles/{userId} | member, profile.read |
| GET /user-profiles | GET /api/user-profiles | admin, profile.read |
| GET /user-profiles/{userId} | GET /api/user-profiles/{userId} | admin, profile.read |
| POST /user-profiles | POST /api/user-profiles | admin, profile.write |
| PUT /user-profiles/{userId} | PUT /api/user-profiles/{userId} | admin, profile.write |
| DELETE /user-profiles/{userId} | DELETE /api/user-profiles/{userId} | admin, profile.write |

The read and write scopes, and the member and admin roles, are exercise-specific choices. Each route uses the gateway's existing Bearer authentication scheme. The /me route reads the sub claim from the validated JWT and uses it as the downstream user ID. Our API takes GUID user IDs, so a test token for /me needs a GUID in sub.

Ocelot also forwards X-UserId and X-Role headers populated from the validated token. The lecturer's gateway removes incoming versions of those headers first. The microservice currently does not use these headers for business decisions; the /me path demonstrates the trusted identity claim without adding a new endpoint.

## Checks

With both applications running, use:

    .\test-week43.ps1

This checks the route behavior, missing and invalid tokens, role and scope restrictions, the CRUD calls and the /me claim transformation. A separate static check is available without Docker:

    python .\tests\check_config.py

A passing static check is not evidence that the gateway can communicate with the API. Full HTTP tests were not run in the environment where these files were prepared.

When everything works, the lecturer's instructions ask for a local commit in the standalone gateway clone, not a push to the lecturer's repository. In that clone, commit the added route file and deletion of the example route. Our week 43 branch stores a copy of the configuration and scripts so they are easy to find later.

## Before using this in a larger deployment

The /faucet endpoint is only a classroom utility and is not a real identity provider. Calling the microservice directly currently bypasses gateway authentication. A later deployment must prevent clients from bypassing the gateway and must decide how downstream services verify trusted identity. Check ports, service discovery, token validation and authorization rules again before integration.
