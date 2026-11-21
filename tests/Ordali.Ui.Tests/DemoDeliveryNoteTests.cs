using System.Net;
using Microsoft.Extensions.Time.Testing;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Demo;
using Ordali.Client.Models;

namespace Ordali.Ui.Tests;

public class DemoDeliveryNoteTests
{
    private static readonly Guid Loading = Guid.Parse("6d3e0f50-0000-4000-8000-100000000002");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly SessionState _session;
    private readonly DemoOrdaliApi _api;

    public DemoDeliveryNoteTests()
    {
        _session = new SessionState(_time);
        _api = new DemoOrdaliApi(_session, _time);
    }

    [Fact]
    public async Task Demo_starts_with_the_note_of_order_10233()
    {
        await SignInAsync("mozo@demo.local");

        var note = Assert.Single(await _api.GetDeliveryNotesAsync());

        Assert.Equal(("ALB-0001", "10233", 1), (note.Code, note.Reference, note.PalletCount));
        Assert.Equal("TOR-M8", Assert.Single(note.Lines).Sku);
    }

    [Fact]
    public async Task The_draft_follows_the_shipments_of_the_load_and_pallets_are_compacted()
    {
        await SignInAsync("encargado@demo.local");
        var notYet = await Assert.ThrowsAsync<ApiException>(() => _api.GetDeliveryNoteDraftAsync(Loading));
        var rice = (await _api.GetStockAsync()).Single(s => s.Sku == "ARR-002");
        await _api.ShipScheduledEventAsync(Loading, new ShipEventInput
        {
            Reference = "PC-10240",
            Lines =
            [
                new() { ProductId = rice.ProductId, FromLocationId = rice.LocationId, Lot = rice.Lot, Quantity = 12 },
                new() { ProductId = rice.ProductId, FromLocationId = rice.LocationId, Lot = rice.Lot, Quantity = 8 },
            ],
        });

        var draft = await _api.GetDeliveryNoteDraftAsync(Loading);
        var note = await _api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            ScheduledEventId = Loading,
            Customer = draft.Customer,
            Reference = draft.Reference,
            VehiclePlate = "  9876 kkl ",
            Lines =
            [
                new() { ProductId = rice.ProductId, Lot = rice.Lot, Quantity = 12, Pallet = 3 },
                new() { ProductId = rice.ProductId, Lot = rice.Lot, Quantity = 8, Pallet = 5 },
            ],
        });
        var twice = await Assert.ThrowsAsync<ApiException>(() => _api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            ScheduledEventId = Loading,
            Customer = "Otro",
            Lines = [new() { ProductId = rice.ProductId, Lot = rice.Lot, Quantity = 1 }],
        }));

        Assert.Equal(HttpStatusCode.Conflict, notYet.Status);
        var line = Assert.Single(draft.Lines);
        Assert.Equal(("Hostelería Centro", "PC-10240", rice.Lot, 20m), (draft.Customer, draft.Reference, line.Lot, line.Quantity));
        Assert.Equal(("ALB-0002", 2, "9876 KKL"), (note.Code, note.PalletCount, note.VehiclePlate));
        Assert.Equal([1, 2], note.Lines.Select(l => l.Pallet));
        Assert.Equal(note.Id, (await _api.GetDeliveryNoteDraftAsync(Loading)).ExistingId);
        Assert.Equal(HttpStatusCode.Conflict, twice.Status);
    }

    [Fact]
    public async Task Lot_tracked_items_need_their_lot()
    {
        await SignInAsync("encargado@demo.local");
        var oil = (await _api.GetProductsAsync()).Single(p => p.Sku == "ACE-001");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            Customer = "Bar La Plaza",
            Lines = [new() { ProductId = oil.Id, Quantity = 6 }],
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("ACE-001", Assert.Single(ex.Errors["Lines"]), StringComparison.Ordinal);
    }

    private async Task SignInAsync(string email) =>
        _session.Set(await _api.LoginAsync(new LoginRequest(email, "Demo1234!")));
}
