using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ConcreteDelivery.Data;

/// <summary>
/// Factory for creating DbContext instances at design time for migrations.
/// </summary>
public class ConcreteDeliveryDbContextFactory : IDesignTimeDbContextFactory<ConcreteDeliveryDbContext>
{
    public ConcreteDeliveryDbContext CreateDbContext(string[] args)
    {
        // Build configuration to read from user secrets
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<ConcreteDeliveryDbContextFactory>()
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ConcreteDeliveryDbContext>();
        
        // Get connection string from configuration (user secrets, appsettings.json, or environment variables)
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=concretedelivery;Username=postgres;Password=postgres";
        
        optionsBuilder.UseNpgsql(connectionString);

        return new ConcreteDeliveryDbContext(optionsBuilder.Options);
    }
}
