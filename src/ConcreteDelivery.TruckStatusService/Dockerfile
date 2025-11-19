# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ConcreteDelivery.TruckStatusService/ConcreteDelivery.TruckStatusService.csproj", "ConcreteDelivery.TruckStatusService/"]
COPY ["src/ConcreteDelivery.Data/ConcreteDelivery.Data.csproj", "ConcreteDelivery.Data/"]
COPY ["src/ConcreteDelivery.Messaging/ConcreteDelivery.Messaging.csproj", "ConcreteDelivery.Messaging/"]
RUN dotnet restore "ConcreteDelivery.TruckStatusService/ConcreteDelivery.TruckStatusService.csproj"

# Copy everything else and build
COPY src/ .
WORKDIR "/src/ConcreteDelivery.TruckStatusService"
RUN dotnet build "ConcreteDelivery.TruckStatusService.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConcreteDelivery.TruckStatusService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create a non-root user
RUN groupadd -r appuser || true && \
    useradd -r -g appuser -u 1001 appuser || true && \
    chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConcreteDelivery.TruckStatusService.dll"]
