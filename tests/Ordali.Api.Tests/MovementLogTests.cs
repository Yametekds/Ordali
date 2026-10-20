using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ordali.Api.Data;

namespace Ordali.Api.Tests;

public class MovementLogTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Logged_movements_cannot_be_changed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdaliDbContext>();
        var movement = await db.StockMovements.FirstAsync();

        movement.Quantity += 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Stale_stock_line_is_rejected()
    {
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var db1 = first.ServiceProvider.GetRequiredService<OrdaliDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<OrdaliDbContext>();
        var id = (await db1.StockLines.FirstAsync()).Id;
        var line1 = await db1.StockLines.SingleAsync(s => s.Id == id);
        var line2 = await db2.StockLines.SingleAsync(s => s.Id == id);

        line1.Quantity -= 1;
        await db1.SaveChangesAsync();
        line2.Quantity -= 1;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }
}
