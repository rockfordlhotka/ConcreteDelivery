# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/ConcreteDelivery.Web/ConcreteDelivery.Web.csproj", "ConcreteDelivery.Web/"]
COPY ["src/ConcreteDelivery.Data/ConcreteDelivery.Data.csproj", "ConcreteDelivery.Data/"]
COPY ["src/ConcreteDelivery.Messaging/ConcreteDelivery.Messaging.csproj", "ConcreteDelivery.Messaging/"]
RUN dotnet restore "ConcreteDelivery.Web/ConcreteDelivery.Web.csproj"

# Copy everything else and build
COPY src/ .
WORKDIR "/src/ConcreteDelivery.Web"
RUN dotnet build "ConcreteDelivery.Web.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ConcreteDelivery.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Create a non-root user (use existing app user if available)
RUN groupadd -r appuser || true && \
    useradd -r -g appuser -u 1001 appuser || true && \
    chown -R appuser:appuser /app
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConcreteDelivery.Web.dll"]
