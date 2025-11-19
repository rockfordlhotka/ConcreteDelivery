FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/ConcreteDelivery.JobWorkflowService/ConcreteDelivery.JobWorkflowService.csproj", "src/ConcreteDelivery.JobWorkflowService/"]
COPY ["src/ConcreteDelivery.Messaging/ConcreteDelivery.Messaging.csproj", "src/ConcreteDelivery.Messaging/"]
COPY ["src/ConcreteDelivery.Data/ConcreteDelivery.Data.csproj", "src/ConcreteDelivery.Data/"]
RUN dotnet restore "./src/ConcreteDelivery.JobWorkflowService/ConcreteDelivery.JobWorkflowService.csproj"
COPY . .
WORKDIR "/src/src/ConcreteDelivery.JobWorkflowService"
RUN dotnet build "./ConcreteDelivery.JobWorkflowService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ConcreteDelivery.JobWorkflowService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConcreteDelivery.JobWorkflowService.dll"]
