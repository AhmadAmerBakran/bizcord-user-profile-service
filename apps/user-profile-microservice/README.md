# User Profile microservice

This folder contains the User Profile microservice for Bizcord.

The current source is the initial ASP.NET Core Web API boilerplate. It includes the standard sample endpoint so the application can be started and tested before profile-specific endpoints are added.

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

The sample `WeatherForecast` endpoint is temporary boilerplate and will be replaced as the User Profile API is implemented.
