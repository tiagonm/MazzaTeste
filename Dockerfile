# syntax=docker/dockerfile:1

# =============================================================================
# Build stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Restore is the slowest step and changes least often, so only the files it actually
# reads are copied first. Editing a .cs file then reuses the cached restore layer;
# without this split every rebuild would re-download the whole package graph.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Mazza.Orders.Domain/Mazza.Orders.Domain.csproj         src/Mazza.Orders.Domain/
COPY src/Mazza.Orders.Application/Mazza.Orders.Application.csproj src/Mazza.Orders.Application/
COPY src/Mazza.Orders.Infrastructure/Mazza.Orders.Infrastructure.csproj src/Mazza.Orders.Infrastructure/
COPY src/Mazza.Orders.Api/Mazza.Orders.Api.csproj               src/Mazza.Orders.Api/

RUN dotnet restore src/Mazza.Orders.Api/Mazza.Orders.Api.csproj

# Now the sources. Everything below this line re-runs on any code change.
COPY .editorconfig ./
COPY src/ src/

# --no-restore because the layer above already did it, and would otherwise reach the
# network again on every build.
RUN dotnet publish src/Mazza.Orders.Api/Mazza.Orders.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# =============================================================================
# Runtime stage
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# curl is here only so HEALTHCHECK below has something to call with. The ASP.NET
# runtime image ships without any HTTP client, which would make the health check
# silently useless.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

# The SQLite file lives on a volume rather than in the image layer, so orders survive
# a container replacement. $APP_UID is the non-root user baked into the .NET images.
RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data

ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__OrdersDatabase="Data Source=/data/orders.db" \
    DOTNET_gcServer=1

EXPOSE 8080

VOLUME ["/data"]

# Never root: a container escape should not come with administrative privileges.
USER $APP_UID

# start-period covers the first-run migration, which is the slowest thing this
# process ever does at startup.
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Mazza.Orders.Api.dll"]
