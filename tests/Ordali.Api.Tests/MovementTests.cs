using System.Net;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class MovementTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Demo_data_has_sites_locations_and_a_movement_history()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var site = Assert.Single(await api.GetSitesAsync());
        var locations = await api.GetLocationsAsync(site.Id);
        var movements = await api.GetMovementsAsync(take: 2);

        Assert.Equal("MAD1", site.Code);
        Assert.Equal(8, locations.Count);
        Assert.Equal(2, movements.Count);
        Assert.True(movements[0].CreatedAt > movements[1].CreatedAt);
    }

    [Fact]
    public async Task Operator_transfer_moves_stock_and_logs_who_did_it()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var movement = await api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Transfer,
            ProductId = DemoIds.Product(DemoIds.Tape),
            FromLocationId = DemoIds.Location(DemoIds.AisleB1),
            ToLocationId = DemoIds.Location(DemoIds.Picking1),
            Quantity = 5,
            Reason = "Reposición",
        });

        Assert.Equal("Javier Ruiz", movement.UserName);
        Assert.Equal("B-01-01", movement.FromLocationCode);
        Assert.Equal("PCK-01", movement.ToLocationCode);
        var stock = await api.GetStockAsync(productId: DemoIds.Product(DemoIds.Tape));
        Assert.Equal(5, stock.Single(s => s.LocationCode == "PCK-01").Quantity);
        Assert.Equal((await api.GetMovementsAsync(take: 1))[0].Id, movement.Id);
    }

    [Fact]
    public async Task Shipping_more_than_available_is_a_conflict()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Shipment,
            ProductId = DemoIds.Product(DemoIds.Screws),
            FromLocationId = DemoIds.Location(DemoIds.Picking2),
            Quantity = 1000,
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.StartsWith("No hay stock suficiente de TOR-M8 en PCK-02", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adjustment_needs_a_location_a_quantity_and_a_reason()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Adjustment,
            ProductId = DemoIds.Product(DemoIds.Tape),
            Quantity = 0,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Equal(["quantity", "reason", "toLocationId"], ex.Errors.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Lot_tracked_product_needs_a_lot()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Transfer,
            ProductId = DemoIds.Product(DemoIds.OliveOil),
            FromLocationId = DemoIds.Location(DemoIds.AisleA1),
            ToLocationId = DemoIds.Location(DemoIds.Picking1),
            Quantity = 10,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("lot", ex.Errors.Keys);
    }

    [Fact]
    public async Task Receipts_cannot_be_posted_as_plain_movements()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Receipt,
            ProductId = DemoIds.Product(DemoIds.Tape),
            ToLocationId = DemoIds.Location(DemoIds.Receiving),
            Quantity = 10,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("type", ex.Errors.Keys);
    }

    [Fact]
    public async Task Negative_adjustment_cannot_leave_negative_stock()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Adjustment,
            ProductId = DemoIds.Product(DemoIds.OliveOil),
            ToLocationId = DemoIds.Location(DemoIds.AisleA1),
            Lot = "L2026-09A",
            Quantity = -10_000,
            Reason = "Recuento",
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
    }
}
