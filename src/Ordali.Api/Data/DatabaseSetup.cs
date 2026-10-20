using Microsoft.EntityFrameworkCore;

namespace Ordali.Api.Data;

public static partial class DatabaseSetup
{
    /// <summary>
    /// SQL Server when a connection string is set; otherwise an in-memory database so
    /// <c>dotnet run</c> works without installing anything else.
    /// </summary>
    public static IServiceCollection AddOrdaliDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Ordali");
        var inMemoryName = configuration.GetValue("Database:InMemoryName", "ordali")!;
        services.AddDbContext<OrdaliDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseInMemoryDatabase(inMemoryName);
            }
            else
            {
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            }
        });
        return services;
    }

    /// <summary>
    /// Applies pending migrations and the demo data when enabled. In production you can turn
    /// it off and run the migrations as a separate deployment step instead.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdaliDbContext>();

        if (db.Database.IsRelational())
        {
            if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
            {
                await db.Database.MigrateAsync();
            }
        }
        else
        {
            LogInMemory(app.Logger);
            await db.Database.EnsureCreatedAsync();
        }

        if (app.Configuration.GetValue("Database:SeedDemoData", true))
        {
            await DemoSeed.SeedAsync(scope.ServiceProvider);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No connection string 'Ordali' configured: using an in-memory database that is lost on restart.")]
    private static partial void LogInMemory(ILogger logger);
}
