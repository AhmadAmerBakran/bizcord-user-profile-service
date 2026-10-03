FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["packages/Shared.Contracts/Shared.Contracts.csproj", "packages/Shared.Contracts/"]
COPY ["src/UserProfile.Api/UserProfile.Api.csproj", "src/UserProfile.Api/"]
COPY ["src/UserProfile.Application/UserProfile.Application.csproj", "src/UserProfile.Application/"]
COPY ["src/UserProfile.Contracts/UserProfile.Contracts.csproj", "src/UserProfile.Contracts/"]
COPY ["src/UserProfile.Domain/UserProfile.Domain.csproj", "src/UserProfile.Domain/"]
COPY ["src/UserProfile.Infrastructure/UserProfile.Infrastructure.csproj", "src/UserProfile.Infrastructure/"]

RUN dotnet restore "src/UserProfile.Api/UserProfile.Api.csproj"

COPY . .
WORKDIR "/src/src/UserProfile.Api"
RUN dotnet build "UserProfile.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "UserProfile.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UserProfile.Api.dll"]
