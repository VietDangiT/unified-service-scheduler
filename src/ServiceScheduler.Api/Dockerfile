# Build context is the repository root:
#   docker build -f src/ServiceScheduler.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Restore first, from the project files only, so the layer is reused until a dependency changes.
# global.json, Directory.Build.props and .editorconfig take part in the build (warnings are errors).
COPY global.json Directory.Build.props .editorconfig ./
COPY src/ServiceScheduler.Domain/ServiceScheduler.Domain.csproj src/ServiceScheduler.Domain/
COPY src/ServiceScheduler.Application/ServiceScheduler.Application.csproj src/ServiceScheduler.Application/
COPY src/ServiceScheduler.Infrastructure/ServiceScheduler.Infrastructure.csproj src/ServiceScheduler.Infrastructure/
COPY src/ServiceScheduler.Api/ServiceScheduler.Api.csproj src/ServiceScheduler.Api/
RUN dotnet restore src/ServiceScheduler.Api/ServiceScheduler.Api.csproj

COPY src/ src/
RUN dotnet publish src/ServiceScheduler.Api/ServiceScheduler.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# The non-root user that ships with the .NET images.
USER $APP_UID

ENTRYPOINT ["dotnet", "ServiceScheduler.Api.dll"]
