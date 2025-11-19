namespace ConcreteDelivery.Data.Entities;

public class Plant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public PlantInventory? Inventory { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
