using System.Net;
using Microsoft.Extensions.Time.Testing;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Demo;
using Ordali.Client.Models;

namespace Ordali.Ui.Tests;

public class DemoCalendarTests
{
    private static readonly Guid ReceivingDock = Guid.Parse("4b8e7a10-0000-4000-8000-100000000001");
    private static readonly Guid Tape = Guid.Parse("4b8e7a10-0000-4000-8000-200000000004");

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly SessionState _session;
    private readonly DemoOrdaliApi _api;

    public DemoCalendarTests()
    {
        _session = new SessionState(_time);
        _api = new DemoOrdaliApi(_session, _time);
    }

    [Fact]
    public async Task Demo_week_matches_the_api_seed()
    {
        await SignInAsync("mozo@demo.local");

        var week = await _api.GetCalendarAsync(_time.GetUtcNow().AddDays(-1), _time.GetUtcNow().AddDays(5));

        Assert.Equal(5, week.Count);
        Assert.Equal("ENTRADA2", Assert.Single(week, e => e.Status == ScheduledEventStatus.Completed).Outcome);
        var forbidden = await Assert.ThrowsAsync<ApiException>(() => _api.CreateScheduledEventAsync(Unloading(10)));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
    }

    [Fact]
    public async Task Weekly_slots_share_a_series_and_a_booked_dock_needs_confirmation()
    {
        await SignInAsync("encargado@demo.local");
        var weekly = Unloading(10);
        weekly.RepeatWeeks = 3;

        var created = await _api.CreateScheduledEventAsync(weekly);
        var clash = await Assert.ThrowsAsync<ApiException>(() => _api.CreateScheduledEventAsync(Unloading(17)));
        var overlap = Unloading(17);
        overlap.AllowOverlap = true;
        var forced = await _api.CreateScheduledEventAsync(overlap);

        Assert.Equal(4, created.Count);
        Assert.Single(created.Select(e => e.SeriesId).Distinct());
        Assert.Equal(HttpStatusCode.Conflict, clash.Status);
        Assert.Single(forced);
    }

    [Fact]
    public async Task Receiving_closes_the_slot_with_its_receipt_code()
    {
        await SignInAsync("encargado@demo.local");
        var slot = (await _api.CreateScheduledEventAsync(Unloading(10)))[0];

        var done = await _api.ReceiveScheduledEventAsync(slot.Id, new ReceiptInput { Lines = [new() { ProductId = Tape, Quantity = 30 }] });

        Assert.Equal(ScheduledEventStatus.Completed, done.Status);
        var receipt = Assert.Single(await _api.GetReceiptsAsync(pendingOnly: true), r => r.Code == done.Outcome);
        Assert.Equal("Cinta · Embalajes Tajo", receipt.Reference);
        await Assert.ThrowsAsync<ApiException>(() => _api.CancelScheduledEventAsync(slot.Id));
    }

    [Fact]
    public async Task Shipping_more_than_there_is_changes_nothing()
    {
        await SignInAsync("encargado@demo.local");
        var outbound = Guid.Parse("6d3e0f50-0000-4000-8000-100000000002");
        var rice = (await _api.GetStockAsync()).Single(s => s.Sku == "ARR-002");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.ShipScheduledEventAsync(outbound, new ShipEventInput
        {
            Lines =
            [
                new() { ProductId = rice.ProductId, FromLocationId = rice.LocationId, Lot = rice.Lot, Quantity = 20 },
                new() { ProductId = rice.ProductId, FromLocationId = rice.LocationId, Lot = rice.Lot, Quantity = 50 },
            ],
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.Equal(rice.Quantity, (await _api.GetStockAsync()).Single(s => s.Sku == "ARR-002").Quantity);
        Assert.Equal(ScheduledEventStatus.Scheduled, (await _api.GetScheduledEventAsync(outbound)).Status);
    }

    private ScheduledEventInput Unloading(int days) => new()
    {
        Type = ScheduledEventType.Inbound,
        Title = "Cinta",
        Partner = "Embalajes Tajo",
        StartsAt = _time.GetUtcNow().AddDays(days),
        EndsAt = _time.GetUtcNow().AddDays(days).AddHours(1),
        DockLocationId = ReceivingDock,
        Lines = [new() { ProductId = Tape, Quantity = 30 }],
    };

    private async Task SignInAsync(string email) =>
        _session.Set(await _api.LoginAsync(new LoginRequest(email, "Demo1234!")));
}
