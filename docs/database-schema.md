# Database Schema

This document defines the PostgreSQL database schema for the Concrete Delivery system.

## Tables

### Trucks

Stores information about delivery trucks in the fleet.

```sql
CREATE TABLE trucks (
    id SERIAL PRIMARY KEY,
    driver_name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Truck Status

Maintains the current operational status of each truck.

```sql
CREATE TABLE truck_status (
    id SERIAL PRIMARY KEY,
    truck_id INTEGER NOT NULL REFERENCES trucks(id),
    status VARCHAR(50) NOT NULL, -- e.g., 'Available', 'EnRoute', 'Delivering', 'Returning'
    current_order_id INTEGER NULL, -- Reference to current order if applicable
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_truck FOREIGN KEY (truck_id) REFERENCES trucks(id) ON DELETE CASCADE
);

-- Index for quick truck status lookups
CREATE INDEX idx_truck_status_truck_id ON truck_status(truck_id);
CREATE INDEX idx_truck_status_status ON truck_status(status);
```

### Orders

Stores customer orders for concrete delivery.

```sql
CREATE TABLE orders (
    id SERIAL PRIMARY KEY,
    customer_name VARCHAR(200) NOT NULL,
    distance_miles INTEGER NOT NULL, -- Distance from plant to delivery location
    status VARCHAR(50) NOT NULL, -- e.g., 'Pending', 'Assigned', 'InTransit', 'Delivered', 'Cancelled'
    plant_id INTEGER NULL, -- Assigned plant
    truck_id INTEGER NULL, -- Assigned truck
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Indexes for common queries
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_orders_truck_id ON orders(truck_id);
CREATE INDEX idx_orders_plant_id ON orders(plant_id);
```

### Plants

Stores information about concrete plants.

```sql
CREATE TABLE plants (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Plant Inventory

Maintains inventory levels for each plant.

```sql
CREATE TABLE plant_inventory (
    id SERIAL PRIMARY KEY,
    plant_id INTEGER NOT NULL REFERENCES plants(id),
    sand_quantity INTEGER NOT NULL DEFAULT 0, -- Units can be tons, cubic yards, etc.
    gravel_quantity INTEGER NOT NULL DEFAULT 0,
    concrete_quantity INTEGER NOT NULL DEFAULT 0,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_plant FOREIGN KEY (plant_id) REFERENCES plants(id) ON DELETE CASCADE,
    CONSTRAINT unique_plant_inventory UNIQUE (plant_id)
);

-- Index for plant inventory lookups
CREATE INDEX idx_plant_inventory_plant_id ON plant_inventory(plant_id);
```

## Relationships

- **Trucks** ↔ **Truck Status**: One-to-many (each truck has status records)
- **Orders** → **Trucks**: Many-to-one (orders can be assigned to trucks)
- **Orders** → **Plants**: Many-to-one (orders can be assigned to plants)
- **Plants** ↔ **Plant Inventory**: One-to-one (each plant has one inventory record)

## Notes

- The `distance_miles` field in the Orders table will be used by the TruckStatusService to simulate travel time
- Status values should be defined as enums or constants in the application code
- The schema uses `SERIAL` for auto-incrementing primary keys
- Timestamps track creation and updates for auditing purposes
- Foreign key constraints ensure referential integrity
- Indexes are added on commonly queried columns for performance

## Sample Data

For demo purposes, you can seed the database with:

- 3-5 trucks with driver names
- 2-3 plants with initial inventory
- Initial truck status records showing all trucks as "Available"
