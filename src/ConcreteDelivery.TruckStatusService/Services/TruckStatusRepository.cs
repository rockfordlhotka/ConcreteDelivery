using System.Diagnostics;
using ConcreteDelivery.Data;
using ConcreteDelivery.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConcreteDelivery.TruckStatusService.Services;

/// <summary>
/// Repository for managing truck and order status in the database
/// </summary>
public class TruckStatusRepository
{
    private readonly IDbContextFactory<ConcreteDeliveryDbContext> _contextFactory;
    private readonly ILogger<TruckStatusRepository> _logger;
    private readonly ActivitySource _activitySource;

    public TruckStatusRepository(
        IDbContextFactory<ConcreteDeliveryDbContext> contextFactory,
        ILogger<TruckStatusRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.TruckStatusService");
    }

    /// <summary>
    /// Updates the truck status and associated order
    /// </summary>
    public async Task<bool> UpdateTruckStatusAsync(
        int truckId, 
        string newStatus, 
        int? orderId = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("UpdateTruckStatus");
        activity?.SetTag("truck.id", truckId);
        activity?.SetTag("status.new", newStatus);
        activity?.SetTag("order.id", orderId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var truckStatus = await context.TruckStatuses
                .FirstOrDefaultAsync(ts => ts.TruckId == truckId, cancellationToken);

            if (truckStatus == null)
            {
                _logger.LogWarning("Truck status not found for truck {TruckId}", truckId);
                return false;
            }

            var previousStatus = truckStatus.Status;
            truckStatus.Status = newStatus;
            truckStatus.CurrentOrderId = orderId;
            truckStatus.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated truck {TruckId} status from {PreviousStatus} to {NewStatus}",
                truckId, previousStatus, newStatus);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating truck status for truck {TruckId}", truckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Updates the order status
    /// </summary>
    public async Task<bool> UpdateOrderStatusAsync(
        int orderId,
        string newStatus,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("UpdateOrderStatus");
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("status.new", newStatus);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var order = await context.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order not found with ID {OrderId}", orderId);
                return false;
            }

            var previousStatus = order.Status;
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated order {OrderId} status from {PreviousStatus} to {NewStatus}",
                orderId, previousStatus, newStatus);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status for order {OrderId}", orderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets the order details for a truck
    /// </summary>
    public async Task<Order?> GetOrderForTruckAsync(
        int truckId,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetOrderForTruck");
        activity?.SetTag("truck.id", truckId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var truckStatus = await context.TruckStatuses
                .Include(ts => ts.CurrentOrder)
                .FirstOrDefaultAsync(ts => ts.TruckId == truckId, cancellationToken);

            return truckStatus?.CurrentOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order for truck {TruckId}", truckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets truck information by ID
    /// </summary>
    public async Task<Truck?> GetTruckAsync(
        int truckId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Trucks
                .Include(t => t.CurrentStatus)
                .FirstOrDefaultAsync(t => t.Id == truckId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting truck {TruckId}", truckId);
            return null;
        }
    }

    /// <summary>
    /// Gets all trucks that are assigned to orders but not yet in active workflow
    /// </summary>
    public async Task<List<(int TruckId, int OrderId, string DriverName)>> GetAssignedTrucksAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            
            return await context.TruckStatuses
                .Where(ts => ts.Status == "Assigned" && ts.CurrentOrderId != null)
                .Include(ts => ts.Truck)
                .Select(ts => new
                {
                    ts.TruckId,
                    OrderId = ts.CurrentOrderId!.Value,
                    ts.Truck.DriverName
                })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => (x.TruckId, x.OrderId, x.DriverName))
                    .ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting assigned trucks");
            return new List<(int, int, string)>();
        }
    }

    /// <summary>
    /// Gets all trucks that are stuck in intermediate states (not Available or Assigned)
    /// </summary>
    public async Task<List<(int TruckId, int? OrderId, string Status, string DriverName)>> GetTrucksInIntermediateStatesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            
            var intermediateStatuses = new[]
            {
                "Loading",
                "EnRoute",
                "AtJobSite",
                "Delivering",
                "Returning",
                "Washing"
            };

            return await context.TruckStatuses
                .Where(ts => intermediateStatuses.Contains(ts.Status))
                .Include(ts => ts.Truck)
                .Select(ts => new
                {
                    ts.TruckId,
                    ts.CurrentOrderId,
                    ts.Status,
                    ts.Truck.DriverName
                })
                .ToListAsync(cancellationToken)
                .ContinueWith(task => task.Result
                    .Select(x => (x.TruckId, x.CurrentOrderId, x.Status, x.DriverName))
                    .ToList(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trucks in intermediate states");
            return new List<(int, int?, string, string)>();
        }
    }

    /// <summary>
    /// Gets the truck ID and status for a given order ID
    /// </summary>
    public async Task<(int TruckId, string Status)?> GetTruckByOrderIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetTruckByOrderId");
        activity?.SetTag("order.id", orderId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var truckStatus = await context.TruckStatuses
                .Where(ts => ts.CurrentOrderId == orderId)
                .Select(ts => new { ts.TruckId, ts.Status })
                .FirstOrDefaultAsync(cancellationToken);

            if (truckStatus == null)
                return null;

            return (truckStatus.TruckId, truckStatus.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting truck for order {OrderId}", orderId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }
}
