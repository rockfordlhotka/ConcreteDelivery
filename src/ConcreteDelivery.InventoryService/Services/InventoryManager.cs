using System.Diagnostics;
using System.Diagnostics.Metrics;
using ConcreteDelivery.Data;
using ConcreteDelivery.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConcreteDelivery.InventoryService.Services;

/// <summary>
/// Manages plant inventory operations with OpenTelemetry instrumentation
/// </summary>
public class InventoryManager
{
    private readonly IDbContextFactory<ConcreteDeliveryDbContext> _contextFactory;
    private readonly ILogger<InventoryManager> _logger;
    private readonly ActivitySource _activitySource;
    private readonly Counter<long> _inventoryDeductionCounter;
    
    // Standard amount of materials loaded per truck
    private const int MaterialsPerTruck = 10;

    public InventoryManager(
        IDbContextFactory<ConcreteDeliveryDbContext> contextFactory,
        ILogger<InventoryManager> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _activitySource = new ActivitySource("ConcreteDelivery.InventoryService");
        
        var meter = new Meter("ConcreteDelivery.InventoryService");
        _inventoryDeductionCounter = meter.CreateCounter<long>(
            "inventory.deductions",
            description: "Number of inventory deductions processed");
    }

    /// <summary>
    /// Deducts materials from plant inventory when a truck loads
    /// </summary>
    public async Task<bool> DeductMaterialsForTruckAsync(string truckId, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("DeductMaterialsForTruck");
        activity?.SetTag("truck.id", truckId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            
            // Parse truck ID to integer
            if (!int.TryParse(truckId, out var truckIdInt))
            {
                _logger.LogWarning("Invalid truck ID format: {TruckId}", truckId);
                return false;
            }

            // Find the truck's current status to get the assigned order
            var truckStatus = await context.TruckStatuses
                .Include(ts => ts.CurrentOrder)
                .FirstOrDefaultAsync(ts => ts.TruckId == truckIdInt, cancellationToken);

            if (truckStatus?.CurrentOrder == null)
            {
                _logger.LogWarning("Truck {TruckId} has no assigned order", truckId);
                return false;
            }

            var plantId = truckStatus.CurrentOrder.PlantId;
            if (plantId == null)
            {
                _logger.LogWarning("Order {OrderId} for truck {TruckId} has no assigned plant", 
                    truckStatus.CurrentOrder.Id, truckId);
                return false;
            }

            activity?.SetTag("plant.id", plantId.Value);
            activity?.SetTag("order.id", truckStatus.CurrentOrder.Id);

            // Get the plant inventory
            var inventory = await context.PlantInventories
                .FirstOrDefaultAsync(pi => pi.PlantId == plantId.Value, cancellationToken);

            if (inventory == null)
            {
                _logger.LogError("No inventory found for plant {PlantId}", plantId.Value);
                return false;
            }

            // Check if there are sufficient materials
            if (inventory.SandQuantity < MaterialsPerTruck ||
                inventory.GravelQuantity < MaterialsPerTruck ||
                inventory.ConcreteQuantity < MaterialsPerTruck)
            {
                _logger.LogWarning(
                    "Insufficient materials at plant {PlantId}. Sand: {Sand}, Gravel: {Gravel}, Concrete: {Concrete}",
                    plantId.Value, inventory.SandQuantity, inventory.GravelQuantity, inventory.ConcreteQuantity);
                return false;
            }

            // Deduct materials
            var previousSand = inventory.SandQuantity;
            var previousGravel = inventory.GravelQuantity;
            var previousConcrete = inventory.ConcreteQuantity;

            inventory.SandQuantity -= MaterialsPerTruck;
            inventory.GravelQuantity -= MaterialsPerTruck;
            inventory.ConcreteQuantity -= MaterialsPerTruck;
            inventory.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            _inventoryDeductionCounter.Add(1, 
                new KeyValuePair<string, object?>("plant.id", plantId.Value),
                new KeyValuePair<string, object?>("truck.id", truckId));

            _logger.LogInformation(
                "Materials deducted for truck {TruckId} at plant {PlantId}. " +
                "Sand: {PrevSand} -> {NewSand}, Gravel: {PrevGravel} -> {NewGravel}, Concrete: {PrevConcrete} -> {NewConcrete}",
                truckId, plantId.Value,
                previousSand, inventory.SandQuantity,
                previousGravel, inventory.GravelQuantity,
                previousConcrete, inventory.ConcreteQuantity);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deducting materials for truck {TruckId}", truckId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Gets current inventory levels for a plant
    /// </summary>
    public async Task<PlantInventory?> GetPlantInventoryAsync(int plantId, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("GetPlantInventory");
        activity?.SetTag("plant.id", plantId);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.PlantInventories
                .FirstOrDefaultAsync(pi => pi.PlantId == plantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for plant {PlantId}", plantId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }
}
