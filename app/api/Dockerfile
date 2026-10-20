# syntax=docker/dockerfile:1
# Multi-stage build: the SDK builds and tests, only the runtime ships.  (M3 slides 40, 46)

# ---- stage 1: build + test
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# project files first → restore is cached until dependencies change
COPY OrdersApi/OrdersApi.csproj OrdersApi/
COPY OrdersApi.Tests/OrdersApi.Tests.csproj OrdersApi.Tests/
RUN dotnet restore OrdersApi.Tests/OrdersApi.Tests.csproj
COPY . .
# a failing test fails the build → no image is produced
RUN dotnet test OrdersApi.Tests --no-restore
RUN dotnet publish OrdersApi -c Release -o /out --no-restore

# ---- stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION \
    ASPNETCORE_URLS=http://+:8080
COPY --from=build /out .
EXPOSE 8080
# non-root user provided by the .NET images
USER $APP_UID
# The aspnet image has no curl/wget → the app checks itself (see --healthcheck in Program.cs).
# Kubernetes ignores HEALTHCHECK and uses httpGet probes instead (M5b).
HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
  CMD ["dotnet", "OrdersApi.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "OrdersApi.dll"]
