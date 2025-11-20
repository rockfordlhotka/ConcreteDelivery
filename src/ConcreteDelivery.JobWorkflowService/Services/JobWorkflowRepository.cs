using ConcreteDelivery.Data;
using ConcreteDelivery.Data.Entities;
using MessagingTruckStatus = ConcreteDelivery.Messaging.Constants.TruckStatus;
using Microsoft.EntityFrameworkCore;

namespace ConcreteDelivery.JobWorkflowService.Services;

/// <summary>
/// Repository for job workflow data operations
/// </summary>
public class JobWorkflowRepository
{
    private readonly IDbContextFactory<ConcreteDeliveryDbContext> _contextFactory;
    private readonly ILogger<JobWorkflowRepository> _logger;

    public JobWorkflowRepository(
        IDbContextFactory<ConcreteDeliveryDbContext> contextFactory,
        ILogger<JobWorkflowRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get all pending orders (status = "Pending")
    /// </summary>
    public async Task<List<Order>> GetPendingOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        return await context.Orders
            .Where(o => o.Status == "Pending")
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Get an available truck (status = "Available") with driver information
    /// </summary>
    public async Task<(int TruckId, string DriverName)?> GetAvailableTruckAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var truck = await context.TruckStatuses
            .Where(ts => ts.Status == "Available" && ts.CurrentOrderId == null)
            .Include(ts => ts.Truck)
            .Select(ts => new { ts.TruckId, ts.Truck.DriverName })
            .FirstOrDefaultAsync(cancellationToken);

        return truck == null ? null : (truck.TruckId, truck.DriverName);
    }

    /// <summary>
    /// Assign a truck to an order
    /// </summary>
    public async Task AssignTruckToOrderAsync(
        int orderId,
        int truckId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        // Update order
        var order = await context.Orders.FindAsync([orderId], cancellationToken);
        if (order != null)
        {
            order.TruckId = truckId;
            order.Status = "Assigned";
            order.UpdatedAt = DateTime.UtcNow;
        }

        // Update truck status
        var truckStatus = await context.TruckStatuses
            .FirstOrDefaultAsync(ts => ts.TruckId == truckId, cancellationToken);
        if (truckStatus != null)
        {
            truckStatus.Status = MessagingTruckStatus.Assigned;
            truckStatus.CurrentOrderId = orderId;
            truckStatus.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Assigned truck {TruckId} to order {OrderId}",
            truckId, orderId);
    }

    /// <summary>
    /// Get order details
    /// </summary>
    public async Task<Order?> GetOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Orders.FindAsync([orderId], cancellationToken);
    }

    /// <summary>
    /// Update order status
    /// </summary>
    public async Task UpdateOrderStatusAsync(
        int orderId,
        string status,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var order = await context.Orders.FindAsync([orderId], cancellationToken);
        if (order != null)
        {
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Updated order {OrderId} status to {Status}",
                orderId, status);
        }
    }

    /// <summary>
    /// Update truck status
    /// </summary>
    public async Task UpdateTruckStatusAsync(
        int truckId,
        string status,
        int? orderId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var truckStatus = await context.TruckStatuses
            .FirstOrDefaultAsync(ts => ts.TruckId == truckId, cancellationToken);
        
        if (truckStatus != null)
        {
            truckStatus.Status = status;
            truckStatus.CurrentOrderId = orderId;
            truckStatus.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Updated truck {TruckId} status to {Status}",
                truckId, status);
        }
    }

    /// <summary>
    /// Get truck ID for an order
    /// </summary>
    public async Task<int?> GetTruckIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        
        var order = await context.Orders
            .Where(o => o.Id == orderId)
            .Select(o => o.TruckId)
            .FirstOrDefaultAsync(cancellationToken);
        
        return order;
    }

    /// <summary>
    /// Get truck details by ID
    /// </summary>
    public async Task<Truck?> GetTruckDetailsAsync(
        int truckId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Trucks.FindAsync([truckId], cancellationToken);
    }
}
