# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS restore
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json CarbonGuard.sln ./
COPY src/CarbonGuard.Core/CarbonGuard.Core.csproj src/CarbonGuard.Core/
COPY src/CarbonGuard.Core/packages.lock.json src/CarbonGuard.Core/
COPY src/CarbonGuard.Api/CarbonGuard.Api.csproj src/CarbonGuard.Api/
COPY src/CarbonGuard.Api/packages.lock.json src/CarbonGuard.Api/
COPY tests/CarbonGuard.Tests/CarbonGuard.Tests.csproj tests/CarbonGuard.Tests/
COPY tests/CarbonGuard.Tests/packages.lock.json tests/CarbonGuard.Tests/
RUN dotnet restore CarbonGuard.sln --locked-mode

FROM restore AS test
COPY . .
RUN dotnet test CarbonGuard.sln --configuration Release --no-restore

FROM test AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish src/CarbonGuard.Api/CarbonGuard.Api.csproj \
    --configuration "${BUILD_CONFIGURATION}" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080

COPY --from=publish --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "CarbonGuard.Api.dll"]
