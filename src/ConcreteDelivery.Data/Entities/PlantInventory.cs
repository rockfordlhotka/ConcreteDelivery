namespace ConcreteDelivery.Data.Entities;

public class PlantInventory
{
    public int Id { get; set; }
    public int PlantId { get; set; }
    public int SandQuantity { get; set; }
    public int GravelQuantity { get; set; }
    public int ConcreteQuantity { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Plant Plant { get; set; } = null!;
}
