using ConcreteDelivery.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConcreteDelivery.Data;

public class ConcreteDeliveryDbContext : DbContext
{
    public ConcreteDeliveryDbContext(DbContextOptions<ConcreteDeliveryDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings => 
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<TruckStatus> TruckStatuses => Set<TruckStatus>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Plant> Plants => Set<Plant>();
    public DbSet<PlantInventory> PlantInventories => Set<PlantInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Truck configuration
        modelBuilder.Entity<Truck>(entity =>
        {
            entity.ToTable("trucks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DriverName)
                .HasColumnName("driver_name")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            entity.HasOne(t => t.CurrentStatus)
                .WithOne(ts => ts.Truck)
                .HasForeignKey<TruckStatus>(ts => ts.TruckId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.Orders)
                .WithOne(o => o.Truck)
                .HasForeignKey(o => o.TruckId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TruckStatus configuration
        modelBuilder.Entity<TruckStatus>(entity =>
        {
            entity.ToTable("truck_status");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TruckId).HasColumnName("truck_id").IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.CurrentOrderId).HasColumnName("current_order_id");
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.TruckId).HasDatabaseName("idx_truck_status_truck_id");
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_truck_status_status");

            // Relationships
            entity.HasOne(ts => ts.CurrentOrder)
                .WithMany()
                .HasForeignKey(ts => ts.CurrentOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Order configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CustomerName)
                .HasColumnName("customer_name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.DistanceMiles)
                .HasColumnName("distance_miles")
                .IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.PlantId).HasColumnName("plant_id");
            entity.Property(e => e.TruckId).HasColumnName("truck_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_orders_status");
            entity.HasIndex(e => e.TruckId).HasDatabaseName("idx_orders_truck_id");
            entity.HasIndex(e => e.PlantId).HasDatabaseName("idx_orders_plant_id");

            // Relationships
            entity.HasOne(o => o.Plant)
                .WithMany(p => p.Orders)
                .HasForeignKey(o => o.PlantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Plant configuration
        modelBuilder.Entity<Plant>(entity =>
        {
            entity.ToTable("plants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            entity.HasOne(p => p.Inventory)
                .WithOne(i => i.Plant)
                .HasForeignKey<PlantInventory>(i => i.PlantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PlantInventory configuration
        modelBuilder.Entity<PlantInventory>(entity =>
        {
            entity.ToTable("plant_inventory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PlantId).HasColumnName("plant_id").IsRequired();
            entity.Property(e => e.SandQuantity)
                .HasColumnName("sand_quantity")
                .HasDefaultValue(0)
                .IsRequired();
            entity.Property(e => e.GravelQuantity)
                .HasColumnName("gravel_quantity")
                .HasDefaultValue(0)
                .IsRequired();
            entity.Property(e => e.ConcreteQuantity)
                .HasColumnName("concrete_quantity")
                .HasDefaultValue(0)
                .IsRequired();
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            entity.HasIndex(e => e.PlantId)
                .IsUnique()
                .HasDatabaseName("idx_plant_inventory_plant_id");
        });

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        var now = DateTime.UtcNow;

        // Seed Plants
        modelBuilder.Entity<Plant>().HasData(
            new Plant { Id = 1, Name = "North Plant", CreatedAt = now },
            new Plant { Id = 2, Name = "South Plant", CreatedAt = now },
            new Plant { Id = 3, Name = "East Plant", CreatedAt = now }
        );

        // Seed Plant Inventories
        modelBuilder.Entity<PlantInventory>().HasData(
            new PlantInventory 
            { 
                Id = 1, 
                PlantId = 1, 
                SandQuantity = 1000, 
                GravelQuantity = 800, 
                ConcreteQuantity = 500,
                UpdatedAt = now 
            },
            new PlantInventory 
            { 
                Id = 2, 
                PlantId = 2, 
                SandQuantity = 1200, 
                GravelQuantity = 900, 
                ConcreteQuantity = 600,
                UpdatedAt = now 
            },
            new PlantInventory 
            { 
                Id = 3, 
                PlantId = 3, 
                SandQuantity = 800, 
                GravelQuantity = 700, 
                ConcreteQuantity = 400,
                UpdatedAt = now 
            }
        );

        // Seed Trucks
        modelBuilder.Entity<Truck>().HasData(
            new Truck { Id = 1, DriverName = "John Smith", CreatedAt = now },
            new Truck { Id = 2, DriverName = "Maria Garcia", CreatedAt = now },
            new Truck { Id = 3, DriverName = "David Chen", CreatedAt = now },
            new Truck { Id = 4, DriverName = "Sarah Johnson", CreatedAt = now },
            new Truck { Id = 5, DriverName = "Michael Brown", CreatedAt = now }
        );

        // Seed Truck Statuses (all available initially)
        modelBuilder.Entity<TruckStatus>().HasData(
            new TruckStatus { Id = 1, TruckId = 1, Status = "Available", UpdatedAt = now },
            new TruckStatus { Id = 2, TruckId = 2, Status = "Available", UpdatedAt = now },
            new TruckStatus { Id = 3, TruckId = 3, Status = "Available", UpdatedAt = now },
            new TruckStatus { Id = 4, TruckId = 4, Status = "Available", UpdatedAt = now },
            new TruckStatus { Id = 5, TruckId = 5, Status = "Available", UpdatedAt = now }
        );
    }
}
