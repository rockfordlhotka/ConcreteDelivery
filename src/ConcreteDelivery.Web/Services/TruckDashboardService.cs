using ConcreteDelivery.Data;
using ConcreteDelivery.Messaging;
using ConcreteDelivery.Messaging.Messages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ConcreteDelivery.Web.Services;

/// <summary>
/// Service that manages truck status data for the dashboard with real-time updates from RabbitMQ
/// </summary>
public class TruckDashboardService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<int, TruckStatusInfo> _truckStatuses = new();
    private readonly List<IMessageConsumer> _consumers = new();
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public event EventHandler? StatusChanged;

    public TruckDashboardService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Initialize the service - load data from database and subscribe to RabbitMQ events
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            // Load initial state from database
            await LoadTruckStatusesFromDatabaseAsync();

            // Subscribe to truck events
            await SubscribeToTruckEventsAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task LoadTruckStatusesFromDatabaseAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ConcreteDeliveryDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var trucks = await dbContext.Trucks
            .Include(t => t.CurrentStatus)
            .OrderBy(t => t.Id)
            .ToListAsync();

        foreach (var truck in trucks)
        {
            var statusInfo = new TruckStatusInfo
            {
                TruckId = truck.Id,
                DriverName = truck.DriverName,
                Status = truck.CurrentStatus?.Status ?? "Unknown",
                LastUpdated = truck.CurrentStatus?.UpdatedAt ?? truck.CreatedAt
            };
            _truckStatuses[truck.Id] = statusInfo;
        }
    }

    private async Task SubscribeToTruckEventsAsync()
    {
        // Create a scope to get required services
        using var scope = _scopeFactory.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<IRabbitMqConnection>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();

        // Subscribe to truck status changed events
        await SubscribeToEventAsync<TruckStatusChangedEvent>(
            connection, 
            loggerFactory,
            "dashboard.truck.status.changed",
            "truck.events",
            "truck.status.changed",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    _truckStatuses.AddOrUpdate(truckId,
                        // Add new
                        new TruckStatusInfo
                        {
                            TruckId = truckId,
                            DriverName = "Unknown",
                            Status = message.NewStatus,
                            LastUpdated = message.CreatedAt
                        },
                        // Update existing
                        (_, existing) =>
                        {
                            existing.Status = message.NewStatus;
                            existing.LastUpdated = message.CreatedAt;
                            return existing;
                        });

                    OnStatusChanged();
                }
                await Task.CompletedTask;
            });

        // Subscribe to materials loaded events
        await SubscribeToEventAsync<MaterialsLoadedEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.materials.loaded",
            "truck.events",
            "truck.materials.loaded",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "Loading", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to departed events
        await SubscribeToEventAsync<DepartedForJobSiteEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.departed",
            "truck.events",
            "truck.departed",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "EnRoute", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to arrived events
        await SubscribeToEventAsync<ArrivedAtJobSiteEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.arrived",
            "truck.events",
            "truck.arrived",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "AtJobSite", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to pouring started events
        await SubscribeToEventAsync<PouringStartedEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.pouring.started",
            "truck.events",
            "truck.pouring.started",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "Delivering", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to pouring completed events
        await SubscribeToEventAsync<PouringCompletedEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.pouring.completed",
            "truck.events",
            "truck.pouring.completed",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "Returning", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to wash started events
        await SubscribeToEventAsync<WashStartedEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.wash.started",
            "truck.events",
            "truck.wash.started",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "Washing", message.CreatedAt);
                }
                await Task.CompletedTask;
            });

        // Subscribe to returned events
        await SubscribeToEventAsync<ReturnedToPlantEvent>(
            connection,
            loggerFactory,
            "dashboard.truck.returned",
            "truck.events",
            "truck.returned",
            async (message) =>
            {
                if (int.TryParse(message.TruckId, out var truckId))
                {
                    UpdateTruckStatus(truckId, "Available", message.CreatedAt);
                }
                await Task.CompletedTask;
            });
    }

    private async Task SubscribeToEventAsync<TMessage>(
        IRabbitMqConnection connection,
        ILoggerFactory loggerFactory,
        string queueName,
        string exchangeName,
        string routingKey,
        Func<TMessage, Task> handler)
        where TMessage : IMessage
    {
        var logger = loggerFactory.CreateLogger<MessageConsumer>();
        var consumer = new MessageConsumer(connection, logger);
        
        await consumer.StartConsumingAsync(
            queueName,
            handler,
            exchangeName,
            routingKey);

        _consumers.Add(consumer);
    }

    private void UpdateTruckStatus(int truckId, string status, DateTime updatedAt)
    {
        _truckStatuses.AddOrUpdate(truckId,
            new TruckStatusInfo
            {
                TruckId = truckId,
                DriverName = "Unknown",
                Status = status,
                LastUpdated = updatedAt
            },
            (_, existing) =>
            {
                existing.Status = status;
                existing.LastUpdated = updatedAt;
                return existing;
            });

        OnStatusChanged();
    }

    public IEnumerable<TruckStatusInfo> GetAllTruckStatuses()
    {
        return _truckStatuses.Values.OrderBy(t => t.TruckId);
    }

    public TruckStatusInfo? GetTruckStatus(int truckId)
    {
        return _truckStatuses.TryGetValue(truckId, out var status) ? status : null;
    }

    private void OnStatusChanged()
    {
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _initLock.Dispose();
        
        foreach (var consumer in _consumers)
        {
            if (consumer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _consumers.Clear();
    }
}

/// <summary>
/// Information about a truck's current status
/// </summary>
public class TruckStatusInfo
{
    public int TruckId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
