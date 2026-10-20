using System.Net;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class ReceiptTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Demo_data_has_entrada2_waiting_with_default_locations()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var pending = await api.GetReceiptsAsync(pendingOnly: true);

        var receipt = Assert.Single(pending, r => r.Code == "ENTRADA2");
        Assert.Equal("REC-01", receipt.DockLocationCode);
        Assert.Equal("A-02-01", receipt.Lines.Single(l => l.Sku == "TOR-M8").DefaultLocationCode);
        Assert.Null(receipt.Lines.Single(l => l.Sku == "PAL-EUR").DefaultLocationCode);
        Assert.Contains(pending, r => r.Code == "ENT-0001" && r.Pending == 24);
    }

    [Fact]
    public async Task Receipt_only_brings_the_goods_to_the_dock()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var before = await DockQuantityAsync(api, DemoIds.Rice);

        var receipt = await api.CreateReceiptAsync(new ReceiptInput
        {
            Code = " entrada7 ",
            Reference = "Albarán 1200",
            Lines =
            [
                new() { ProductId = DemoIds.Product(DemoIds.Rice), Lot = "L2026-10D", Quantity = 30 },
                new() { ProductId = DemoIds.Product(DemoIds.Rice), Lot = "L2026-10D", Quantity = 10 },
            ],
        });

        Assert.Equal("ENTRADA7", receipt.Code);
        Assert.Equal(ReceiptStatus.Pending, receipt.Status);
        var line = Assert.Single(receipt.Lines);
        Assert.Equal(40, line.Pending);
        Assert.Equal(before + 40, await DockQuantityAsync(api, DemoIds.Rice));
        var logged = (await api.GetMovementsAsync(take: 1))[0];
        Assert.Equal((MovementType.Receipt, "ENTRADA7", "REC-01"), (logged.Type, logged.Reference, logged.ToLocationCode));
        Assert.Contains(await api.GetReceiptsAsync(pendingOnly: true), r => r.Id == receipt.Id);
    }

    [Fact]
    public async Task Put_away_moves_from_the_dock_until_the_receipt_is_complete()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var receipt = await api.CreateReceiptAsync(new ReceiptInput
        {
            Code = "ENTRADA8",
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 10 }],
        });

        var partial = await api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = DemoIds.Product(DemoIds.Tape), ToLocationId = DemoIds.Location(DemoIds.AisleB1), Quantity = 6,
        });
        var done = await api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = DemoIds.Product(DemoIds.Tape), ToLocationId = DemoIds.Location(DemoIds.Picking1), Quantity = 4,
        });

        Assert.Equal(4, Assert.Single(partial.Lines).Pending);
        Assert.Equal(ReceiptStatus.Completed, done.Status);
        Assert.DoesNotContain(await api.GetReceiptsAsync(pendingOnly: true), r => r.Id == receipt.Id);
        var transfers = (await api.GetMovementsAsync(take: 2)).ToList();
        Assert.All(transfers, m => Assert.Equal((MovementType.Transfer, "ENTRADA8", "REC-01"), (m.Type, m.Reference, m.FromLocationCode)));
    }

    [Fact]
    public async Task Put_away_cannot_move_more_than_is_left()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var receipt = await api.CreateReceiptAsync(new ReceiptInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Screws), Quantity = 5 }],
        });

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = DemoIds.Product(DemoIds.Screws), ToLocationId = DemoIds.Location(DemoIds.AisleA3), Quantity = 6,
        }));

        Assert.StartsWith("ENT-", receipt.Code, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Equal("Solo quedan 5 por ubicar.", ex.Message);
    }

    [Fact]
    public async Task Put_away_rejects_items_that_are_not_in_the_receipt_and_the_dock_itself()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var receipt = await api.CreateReceiptAsync(new ReceiptInput { Lines = [new() { ProductId = DemoIds.Product(DemoIds.Pallet), Quantity = 2 }] });

        var wrongItem = await Assert.ThrowsAsync<ApiException>(() => api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = DemoIds.Product(DemoIds.Tape), ToLocationId = DemoIds.Location(DemoIds.AisleB1), Quantity = 1,
        }));
        var toDock = await Assert.ThrowsAsync<ApiException>(() => api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = DemoIds.Product(DemoIds.Pallet), ToLocationId = receipt.DockLocationId, Quantity = 1,
        }));

        Assert.Contains("productId", wrongItem.Errors.Keys);
        Assert.Contains("toLocationId", toDock.Errors.Keys);
    }

    [Fact]
    public async Task Receipt_codes_are_unique_and_lots_are_required()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var duplicate = await Assert.ThrowsAsync<ApiException>(() => api.CreateReceiptAsync(new ReceiptInput
        {
            Code = "entrada2",
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1 }],
        }));
        var noLot = await Assert.ThrowsAsync<ApiException>(() => api.CreateReceiptAsync(new ReceiptInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Gloves), Quantity = 1 }],
        }));

        Assert.Contains("code", duplicate.Errors.Keys);
        Assert.Contains("lines", noLot.Errors.Keys);
    }

    [Fact]
    public async Task Read_only_profiles_cannot_record_receipts()
    {
        var admin = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);
        var template = (await admin.GetTemplatesAsync()).Single(t => t.Name == "Solo consulta");
        await admin.CreateUserAsync(new NewUserInput { Name = "Eva Ramos", Email = "eva.ramos@demo.local", Password = OrdaliApiFactory.Password, TemplateId = template.Id });
        var viewer = await factory.SignInAsync("eva.ramos@demo.local");

        var ex = await Assert.ThrowsAsync<ApiException>(() => viewer.CreateReceiptAsync(new ReceiptInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1 }],
        }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
        Assert.NotEmpty(await viewer.GetReceiptsAsync());
    }

    private static async Task<decimal> DockQuantityAsync(Ordali.Client.Http.HttpOrdaliApi api, int product) =>
        (await api.GetStockAsync(DemoIds.Product(product), DemoIds.Location(DemoIds.Receiving))).Sum(s => s.Quantity);
}
