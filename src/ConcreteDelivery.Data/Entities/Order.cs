namespace ConcreteDelivery.Data.Entities;

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int DistanceMiles { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Assigned, InTransit, Delivered, Cancelled
    public int? PlantId { get; set; }
    public int? TruckId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Plant? Plant { get; set; }
    public Truck? Truck { get; set; }
}
