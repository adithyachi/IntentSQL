using BizPulse.AI.POC.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "ConnectionStrings__DefaultConnection environment variable is not configured.");

    return 1;
}

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

optionsBuilder.UseNpgsql(connectionString);

await using var dbContext = new AppDbContext(optionsBuilder.Options);

Console.WriteLine("Connecting to PostgreSQL...");

if (!await dbContext.Database.CanConnectAsync())
{
    Console.Error.WriteLine("Could not connect to PostgreSQL.");
    return 1;
}

Console.WriteLine("Connected to PostgreSQL.");
Console.WriteLine("Starting database seed...");

await DatabaseSeeder.SeedAsync(dbContext);

Console.WriteLine("Database seed completed successfully.");

return 0;