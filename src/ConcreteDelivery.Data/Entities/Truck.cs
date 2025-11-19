namespace ConcreteDelivery.Data.Entities;

public class Truck
{
    public int Id { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public TruckStatus? CurrentStatus { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
