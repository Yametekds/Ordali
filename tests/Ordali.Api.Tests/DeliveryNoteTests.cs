using System.Net;
using Ordali.Client;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class DeliveryNoteTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    private const int ShippingDock = 2;
    private const string OilLot = "L2026-09A";

    // Far from the calendar tests' days so the dock is always free.
    private static DateTimeOffset Day(int n) => new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(70 + n);

    [Fact]
    public async Task The_draft_of_a_recorded_load_lists_what_left_with_its_lots()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = await ShippedLoadAsync(api, Day(0), "Pedido cliente 30001");

        var draft = await api.GetDeliveryNoteDraftAsync(slot.Id);

        Assert.Null(draft.ExistingId);
        Assert.Equal(("Bar La Plaza", "PC-30001"), (draft.Customer, draft.Reference));
        // Oil picked from two locations is one line on paper.
        Assert.Collection(
            draft.Lines.OrderBy(l => l.Sku),
            oil => Assert.Equal(("ACE-001", OilLot, 15m, 1), (oil.Sku, oil.Lot, oil.Quantity, oil.Pallet)),
            screws => Assert.Equal(("TOR-M8", (string?)null, 2m, 1), (screws.Sku, screws.Lot, screws.Quantity, screws.Pallet)));
    }

    [Fact]
    public async Task A_note_numbers_its_pallets_from_one_and_a_load_gets_only_one()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var supervisor = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = await ShippedLoadAsync(supervisor, Day(1), "Pedido cliente 30002");

        var note = await api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            ScheduledEventId = slot.Id,
            Customer = "Bar La Plaza",
            Address = "Calle Mayor 4, Getafe",
            Carrier = "Transportes Ruiz",
            VehiclePlate = "1234 bcd",
            Reference = "PC-30002",
            Lines =
            [
                new() { ProductId = DemoIds.Product(DemoIds.Screws), Quantity = 2, Pallet = 4 },
                new() { ProductId = DemoIds.Product(DemoIds.OliveOil), Lot = OilLot, Quantity = 10, Pallet = 2 },
                new() { ProductId = DemoIds.Product(DemoIds.OliveOil), Lot = OilLot, Quantity = 5, Pallet = 2 },
            ],
        });

        Assert.StartsWith("ALB-", note.Code, StringComparison.Ordinal);
        Assert.Equal((2, "1234 BCD", "Javier Ruiz"), (note.PalletCount, note.VehiclePlate, note.UserName));
        Assert.Collection(
            note.Lines,
            oil => Assert.Equal(("ACE-001", 15m, 1), (oil.Sku, oil.Quantity, oil.Pallet)),
            screws => Assert.Equal(("TOR-M8", 2m, 2), (screws.Sku, screws.Quantity, screws.Pallet)));

        var draft = await api.GetDeliveryNoteDraftAsync(slot.Id);
        Assert.Equal(note.Id, draft.ExistingId);
        Assert.Equal(note.Id, Assert.Single(await api.GetDeliveryNotesAsync(slot.Id)).Id);
        Assert.Equal(note, await api.GetDeliveryNoteAsync(note.Id), new NoteComparer());

        var again = await Assert.ThrowsAsync<ApiException>(() => api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            ScheduledEventId = slot.Id,
            Customer = "Bar La Plaza",
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Screws), Quantity = 1 }],
        }));
        Assert.Equal(HttpStatusCode.Conflict, again.Status);
    }

    [Fact]
    public async Task A_note_without_a_load_is_allowed_but_its_lines_are_checked()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var invalid = await Assert.ThrowsAsync<ApiException>(() => api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            Customer = "Hostelería Centro",
            Lines =
            [
                new() { ProductId = DemoIds.Product(DemoIds.OliveOil), Quantity = 6 },
                new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 0 },
                new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1, Pallet = 0 },
                new() { ProductId = Guid.NewGuid(), Quantity = 1 },
            ],
        }));
        var empty = await Assert.ThrowsAsync<ApiException>(() => api.CreateDeliveryNoteAsync(new DeliveryNoteInput { Customer = "Hostelería Centro" }));
        var noCustomer = await Assert.ThrowsAsync<ApiException>(() => api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1 }],
        }));

        Assert.Equal(4, invalid.Errors["lines"].Length);
        Assert.Contains("lines", empty.Errors.Keys);
        Assert.Contains("customer", noCustomer.Errors.Keys);

        var note = await api.CreateDeliveryNoteAsync(new DeliveryNoteInput
        {
            Customer = "Hostelería Centro",
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 12 }],
        });
        Assert.Equal((null, 1), (note.ScheduledEventId, note.PalletCount));
        Assert.Equal(note.Id, (await api.GetDeliveryNotesAsync())[0].Id);
    }

    [Fact]
    public async Task Only_a_recorded_load_has_a_draft()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = (await api.CreateScheduledEventAsync(Loading(Day(2), "Pedido cliente 30003")))[0];

        var notYet = await Assert.ThrowsAsync<ApiException>(() => api.GetDeliveryNoteDraftAsync(slot.Id));
        var missing = await Assert.ThrowsAsync<ApiException>(() => api.GetDeliveryNoteDraftAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, notYet.Status);
        Assert.Equal(HttpStatusCode.NotFound, missing.Status);
    }

    private static async Task<ScheduledEvent> ShippedLoadAsync(HttpOrdaliApi api, DateTimeOffset day, string title)
    {
        var slot = (await api.CreateScheduledEventAsync(Loading(day, title)))[0];
        return await api.ShipScheduledEventAsync(slot.Id, new ShipEventInput
        {
            Reference = title.Replace("Pedido cliente ", "PC-", StringComparison.Ordinal),
            Lines =
            [
                new() { ProductId = DemoIds.Product(DemoIds.OliveOil), FromLocationId = DemoIds.Location(DemoIds.AisleA1), Lot = OilLot, Quantity = 10 },
                new() { ProductId = DemoIds.Product(DemoIds.OliveOil), FromLocationId = DemoIds.Location(DemoIds.Picking1), Lot = OilLot, Quantity = 5 },
                new() { ProductId = DemoIds.Product(DemoIds.Screws), FromLocationId = DemoIds.Location(DemoIds.AisleA3), Quantity = 2 },
            ],
        });
    }

    private static ScheduledEventInput Loading(DateTimeOffset day, string title) => new()
    {
        Type = ScheduledEventType.Outbound,
        Title = title,
        Partner = "Bar La Plaza",
        StartsAt = day.AddHours(9),
        EndsAt = day.AddHours(10),
        DockLocationId = DemoIds.Location(ShippingDock),
        Lines = [new() { ProductId = DemoIds.Product(DemoIds.OliveOil), Quantity = 15 }],
    };

    private sealed class NoteComparer : IEqualityComparer<DeliveryNote>
    {
        public bool Equals(DeliveryNote? x, DeliveryNote? y) =>
            x is not null && y is not null && x.Id == y.Id && x.Code == y.Code && x.Lines.SequenceEqual(y.Lines);

        public int GetHashCode(DeliveryNote obj) => obj.Id.GetHashCode();
    }
}
