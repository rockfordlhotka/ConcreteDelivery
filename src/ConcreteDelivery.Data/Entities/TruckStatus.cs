namespace ConcreteDelivery.Data.Entities;

public class TruckStatus
{
    public int Id { get; set; }
    public int TruckId { get; set; }
    public string Status { get; set; } = string.Empty; // Available, EnRoute, Delivering, Returning
    public int? CurrentOrderId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Truck Truck { get; set; } = null!;
    public Order? CurrentOrder { get; set; }
}
