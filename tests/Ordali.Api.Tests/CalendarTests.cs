using System.Net;
using Ordali.Client;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class CalendarTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    private const int ShippingDock = 2;

    // Each test books its own day far ahead so they don't share a dock slot.
    private static DateTimeOffset Day(int n) => new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(40 + n);

    [Fact]
    public async Task Demo_calendar_shows_the_week_and_the_unloading_that_became_entrada2()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);

        var events = await api.GetCalendarAsync(today.AddDays(-1), today.AddDays(5));

        var done = Assert.Single(events, e => e.Status == ScheduledEventStatus.Completed);
        Assert.Equal(("ENTRADA2", "REC-01"), (done.Outcome, done.DockLocationCode));
        Assert.Contains(events, e => e.Type == ScheduledEventType.Outbound && e.Lines.Any(l => l.Sku == "ARR-002"));
        Assert.Contains(events, e => e.Type == ScheduledEventType.Other && e.Lines.Count == 0);
        Assert.Equal(events.OrderBy(e => e.StartsAt).Select(e => e.Id), events.Select(e => e.Id));
    }

    [Fact]
    public async Task Pickers_see_the_calendar_but_cannot_schedule()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateScheduledEventAsync(Unloading(Day(0))));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task A_weekly_slot_creates_one_entry_per_week_in_the_same_series()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var input = Unloading(Day(1));
        input.RepeatWeeks = 2;

        var created = await api.CreateScheduledEventAsync(input);

        Assert.Equal(3, created.Count);
        Assert.Single(created.Select(e => e.SeriesId).Distinct());
        Assert.NotNull(created[0].SeriesId);
        Assert.Equal([Day(1).AddHours(8), Day(8).AddHours(8), Day(15).AddHours(8)], created.Select(e => e.StartsAt));
        Assert.All(created, e => Assert.Equal(("Marta Gil", ScheduledEventStatus.Scheduled, 30m), (e.CreatedByName, e.Status, e.Lines.Single().Quantity)));
    }

    [Fact]
    public async Task A_booked_dock_asks_for_confirmation_before_double_booking()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        await api.CreateScheduledEventAsync(Unloading(Day(2)));
        var overlapping = Unloading(Day(2).AddMinutes(30));

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateScheduledEventAsync(overlapping));
        overlapping.AllowOverlap = true;
        var forced = await api.CreateScheduledEventAsync(overlapping);

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.Contains("REC-01", ex.Message, StringComparison.Ordinal);
        Assert.Single(forced);
    }

    [Fact]
    public async Task Unloading_books_a_receiving_dock_and_needs_valid_times()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var wrongDock = Unloading(Day(3));
        wrongDock.DockLocationId = DemoIds.Location(ShippingDock);
        var backwards = Unloading(Day(3));
        backwards.EndsAt = backwards.StartsAt.AddHours(-1);

        var dockError = await Assert.ThrowsAsync<ApiException>(() => api.CreateScheduledEventAsync(wrongDock));
        var timeError = await Assert.ThrowsAsync<ApiException>(() => api.CreateScheduledEventAsync(backwards));

        Assert.Contains("dockLocationId", dockError.Errors.Keys);
        Assert.Contains("endsAt", timeError.Errors.Keys);
    }

    [Fact]
    public async Task Receiving_an_unloading_records_the_receipt_on_the_dock_and_closes_it()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = (await api.CreateScheduledEventAsync(Unloading(Day(4))))[0];

        var done = await api.ReceiveScheduledEventAsync(slot.Id, new ReceiptInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 28 }],
        });

        Assert.Equal(ScheduledEventStatus.Completed, done.Status);
        Assert.StartsWith("ENT-", done.Outcome, StringComparison.Ordinal);
        var receipt = Assert.Single(await api.GetReceiptsAsync(pendingOnly: true), r => r.Code == done.Outcome);
        Assert.Equal(("Descarga de cinta · Embalajes Tajo", 28m), (receipt.Reference, receipt.Pending));
        var again = await Assert.ThrowsAsync<ApiException>(() => api.ReceiveScheduledEventAsync(slot.Id, new ReceiptInput
        {
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1 }],
        }));
        Assert.Equal(HttpStatusCode.Conflict, again.Status);
    }

    [Fact]
    public async Task Loading_ships_each_line_from_its_location_or_nothing_at_all()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = (await api.CreateScheduledEventAsync(new ScheduledEventInput
        {
            Type = ScheduledEventType.Outbound,
            Title = "Pedido cliente 20001",
            Partner = "Ferretería Getafe",
            StartsAt = Day(5).AddHours(12),
            EndsAt = Day(5).AddHours(13),
            DockLocationId = DemoIds.Location(ShippingDock),
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Screws), Quantity = 3 }],
        }))[0];
        var before = await QuantityAsync(api, DemoIds.Screws, DemoIds.AisleA3);

        var tooMuch = await Assert.ThrowsAsync<ApiException>(() => api.ShipScheduledEventAsync(slot.Id, new ShipEventInput
        {
            Lines =
            [
                new() { ProductId = DemoIds.Product(DemoIds.Screws), FromLocationId = DemoIds.Location(DemoIds.AisleA3), Quantity = 3 },
                new() { ProductId = DemoIds.Product(DemoIds.Tape), FromLocationId = DemoIds.Location(DemoIds.AisleB1), Quantity = 9999 },
            ],
        }));
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.Status);
        Assert.Equal(before, await QuantityAsync(api, DemoIds.Screws, DemoIds.AisleA3));
        Assert.Equal(ScheduledEventStatus.Scheduled, (await api.GetScheduledEventAsync(slot.Id)).Status);

        var done = await api.ShipScheduledEventAsync(slot.Id, new ShipEventInput
        {
            Reference = "ALB-20001",
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Screws), FromLocationId = DemoIds.Location(DemoIds.AisleA3), Quantity = 3 }],
        });

        Assert.Equal((ScheduledEventStatus.Completed, "ALB-20001"), (done.Status, done.Outcome));
        Assert.Equal(before - 3, await QuantityAsync(api, DemoIds.Screws, DemoIds.AisleA3));
        var logged = (await api.GetMovementsAsync(take: 1))[0];
        Assert.Equal((MovementType.Shipment, "ALB-20001", "Ferretería Getafe"), (logged.Type, logged.Reference, logged.Reason));
    }

    [Fact]
    public async Task Only_other_entries_are_marked_done_and_cancelled_ones_cannot_change()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var unloading = (await api.CreateScheduledEventAsync(Unloading(Day(6))))[0];
        var count = (await api.CreateScheduledEventAsync(new ScheduledEventInput
        {
            Type = ScheduledEventType.Other,
            Title = "Recuento pasillo B",
            StartsAt = Day(6).AddHours(6),
            EndsAt = Day(6).AddHours(8),
            Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 1 }],
        }))[0];

        var notOther = await Assert.ThrowsAsync<ApiException>(() => api.CompleteScheduledEventAsync(unloading.Id));
        var counted = await api.CompleteScheduledEventAsync(count.Id);
        var cancelled = await api.CancelScheduledEventAsync(unloading.Id);
        var edit = await Assert.ThrowsAsync<ApiException>(() => api.UpdateScheduledEventAsync(unloading.Id, Unloading(Day(7))));

        Assert.Equal(HttpStatusCode.Conflict, notOther.Status);
        Assert.Equal((ScheduledEventStatus.Completed, "Marta Gil"), (counted.Status, counted.CompletedByName));
        Assert.Empty(count.Lines);
        Assert.Equal(ScheduledEventStatus.Cancelled, cancelled.Status);
        Assert.Equal(HttpStatusCode.Conflict, edit.Status);
    }

    [Fact]
    public async Task Moving_a_slot_keeps_it_and_replaces_its_goods()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);
        var slot = (await api.CreateScheduledEventAsync(Unloading(Day(9))))[0];
        var input = ScheduledEventInput.From(slot);
        input.StartsAt = input.StartsAt.AddHours(2);
        input.EndsAt = input.EndsAt.AddHours(2);
        input.Lines = [new() { ProductId = DemoIds.Product(DemoIds.Pallet), Quantity = 12 }];

        var moved = await api.UpdateScheduledEventAsync(slot.Id, input);

        Assert.Equal(slot.Id, moved.Id);
        Assert.Equal(Day(9).AddHours(10), moved.StartsAt);
        Assert.Equal("PAL-EUR", Assert.Single(moved.Lines).Sku);
    }

    private static ScheduledEventInput Unloading(DateTimeOffset day) => new()
    {
        Type = ScheduledEventType.Inbound,
        Title = "Descarga de cinta",
        Partner = "Embalajes Tajo",
        StartsAt = day.AddHours(8),
        EndsAt = day.AddHours(9),
        DockLocationId = DemoIds.Location(DemoIds.Receiving),
        Lines = [new() { ProductId = DemoIds.Product(DemoIds.Tape), Quantity = 30 }],
    };

    private static async Task<decimal> QuantityAsync(HttpOrdaliApi api, int product, int location) =>
        (await api.GetStockAsync(DemoIds.Product(product), DemoIds.Location(location))).Sum(s => s.Quantity);
}
