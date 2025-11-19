# ConcreteDelivery.Data

This project contains the Entity Framework Core data layer for the Concrete Delivery system.

## Database Schema

The database includes the following tables:

- **trucks** - Delivery trucks with driver information
- **truck_status** - Current status of each truck (Available, EnRoute, Delivering, Returning)
- **orders** - Customer orders with delivery information
- **plants** - Concrete production plants
- **plant_inventory** - Inventory levels (sand, gravel, concrete) for each plant

See [database-schema.md](../../docs/database-schema.md) for detailed schema documentation.

## Prerequisites

- .NET 10 SDK
- PostgreSQL database server
- Entity Framework Core tools: `dotnet tool install --global dotnet-ef`

## Configuration

Set the connection string via environment variable:

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Database=concretedelivery;Username=postgres;Password=your_password"
```

Or update the default in `ConcreteDeliveryDbContextFactory.cs`.

## Apply Migrations

To create/update the database schema:

```bash
cd src/ConcreteDelivery.Data
dotnet ef database update
```

This will:
1. Create all tables with proper relationships and indexes
2. Seed initial data:
   - 3 plants (North Plant, South Plant, East Plant)
   - Plant inventories with sand, gravel, and concrete quantities
   - 5 trucks with drivers (John Smith, Maria Garcia, David Chen, Sarah Johnson, Michael Brown)
   - Initial truck status records (all available)

## Create New Migration

After modifying entities or DbContext:

```bash
cd src/ConcreteDelivery.Data
dotnet ef migrations add YourMigrationName
```

## Remove Last Migration

```bash
cd src/ConcreteDelivery.Data
dotnet ef migrations remove
```

## Usage in Application

Register the DbContext in your service configuration:

```csharp
services.AddDbContext<ConcreteDeliveryDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
```

## Seed Data

The initial migration includes seed data:

**Plants:**
- North Plant (ID: 1) - Sand: 1000, Gravel: 800, Concrete: 500
- South Plant (ID: 2) - Sand: 1200, Gravel: 900, Concrete: 600
- East Plant (ID: 3) - Sand: 800, Gravel: 700, Concrete: 400

**Trucks:**
- Truck 1 - John Smith (Available)
- Truck 2 - Maria Garcia (Available)
- Truck 3 - David Chen (Available)
- Truck 4 - Sarah Johnson (Available)
- Truck 5 - Michael Brown (Available)

## Entity Relationships

- Trucks have a one-to-one relationship with TruckStatus
- Orders can be assigned to a Truck (many-to-one)
- Orders can be assigned to a Plant (many-to-one)
- Plants have a one-to-one relationship with PlantInventory

## Status Values

**Truck Status:**
- Available
- EnRoute
- Delivering
- Returning

**Order Status:**
- Pending
- Assigned
- InTransit
- Delivered
- Cancelled
