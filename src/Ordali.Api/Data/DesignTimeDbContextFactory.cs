using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordali.Api.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model for SQL Server without a running database.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrdaliDbContext>
{
    public OrdaliDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrdaliDbContext>()
            .UseSqlServer("Server=localhost;Database=Ordali;Trusted_Connection=True;TrustServerCertificate=True")
            .Options);
}
