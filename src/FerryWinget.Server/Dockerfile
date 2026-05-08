# ============================================================
# Stage 1: Build and publish the application
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files first for better layer caching
COPY ["src/FerryWinget.Core/FerryWinget.Core.csproj", "src/FerryWinget.Core/"]
COPY ["src/FerryWinget.Server/FerryWinget.Server.csproj", "src/FerryWinget.Server/"]

# Restore NuGet packages
RUN dotnet restore "src/FerryWinget.Server/FerryWinget.Server.csproj"

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR "/src/src/FerryWinget.Server"
RUN dotnet publish "FerryWinget.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ============================================================
# Stage 2: Final runtime image
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-azurelinux3.0 AS final

WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy default config
COPY config.yaml .

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

# Install curl for health checks
RUN tdnf update -y && tdnf install -y curl && tdnf clean all

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/api/information || exit 1

# Non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "FerryWinget.Server.dll"]
