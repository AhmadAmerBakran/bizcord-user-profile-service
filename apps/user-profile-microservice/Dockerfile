FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["packages/Shared.Contracts/Shared.Contracts.csproj", "packages/Shared.Contracts/"]
COPY ["apps/user-profile-microservice/src/UserProfile.Api/UserProfile.Api.csproj", "apps/user-profile-microservice/src/UserProfile.Api/"]
COPY ["apps/user-profile-microservice/src/UserProfile.Application/UserProfile.Application.csproj", "apps/user-profile-microservice/src/UserProfile.Application/"]
COPY ["apps/user-profile-microservice/src/UserProfile.Contracts/UserProfile.Contracts.csproj", "apps/user-profile-microservice/src/UserProfile.Contracts/"]
COPY ["apps/user-profile-microservice/src/UserProfile.Domain/UserProfile.Domain.csproj", "apps/user-profile-microservice/src/UserProfile.Domain/"]
COPY ["apps/user-profile-microservice/src/UserProfile.Infrastructure/UserProfile.Infrastructure.csproj", "apps/user-profile-microservice/src/UserProfile.Infrastructure/"]

RUN dotnet restore "apps/user-profile-microservice/src/UserProfile.Api/UserProfile.Api.csproj"

COPY . .
WORKDIR "/src/apps/user-profile-microservice/src/UserProfile.Api"
RUN dotnet build "UserProfile.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UserProfile.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserProfile.Api.dll"]
