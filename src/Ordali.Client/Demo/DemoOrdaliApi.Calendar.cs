using System.Net;
using Ordali.Client.Models;

namespace Ordali.Client.Demo;

// The calendar of the demo mode, with the same rules as the API's CalendarService.
public sealed partial class DemoOrdaliApi
{
    private static readonly TimeSpan LongestEvent = TimeSpan.FromDays(7);
    private readonly List<ScheduledEvent> _events = [];

    public Task<IReadOnlyList<ScheduledEvent>> GetCalendarAsync(DateTimeOffset start, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.CalendarView);
        if (until <= start || until - start > TimeSpan.FromDays(100))
        {
            throw Invalid("to", "Pide como mucho 100 días de calendario.");
        }

        lock (_lock)
        {
            return Result<IReadOnlyList<ScheduledEvent>>([.. _events.Where(e => e.StartsAt < until && e.EndsAt > start).OrderBy(e => e.StartsAt)]);
        }
    }

    public Task<ScheduledEvent> GetScheduledEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.CalendarView);
        lock (_lock)
        {
            return Result(FindEvent(id));
        }
    }

    public Task<IReadOnlyList<ScheduledEvent>> CreateScheduledEventAsync(ScheduledEventInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.CalendarEdit);
        lock (_lock)
        {
            var lines = ValidateEvent(input, isNew: true);
            var seriesId = input.RepeatWeeks > 0 ? Guid.NewGuid() : (Guid?)null;
            var now = _time.GetUtcNow();
            var created = Enumerable.Range(0, input.RepeatWeeks + 1)
                .Select(week => ToEvent(Guid.NewGuid(), input, lines, TimeSpan.FromDays(7 * week)) with
                {
                    SeriesId = seriesId,
                    CreatedByName = user.Name,
                    CreatedAt = now,
                })
                .ToList();
            if (!input.AllowOverlap)
            {
                created.ForEach(EnsureDockFree);
            }

            _events.AddRange(created);
            return Result<IReadOnlyList<ScheduledEvent>>(created);
        }
    }

    public Task<ScheduledEvent> UpdateScheduledEventAsync(Guid id, ScheduledEventInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.CalendarEdit);
        lock (_lock)
        {
            var current = FindOpenEvent(id);
            var lines = ValidateEvent(input, isNew: false);
            var updated = ToEvent(id, input, lines, TimeSpan.Zero) with
            {
                SeriesId = current.SeriesId,
                CreatedByName = current.CreatedByName,
                CreatedAt = current.CreatedAt,
            };
            if (!input.AllowOverlap)
            {
                EnsureDockFree(updated);
            }

            return Result(Replace(updated));
        }
    }

    public Task<ScheduledEvent> CancelScheduledEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.CalendarEdit);
        lock (_lock)
        {
            return Result(Replace(Close(FindOpenEvent(id), ScheduledEventStatus.Cancelled, user.Name, null)));
        }
    }

    public Task<ScheduledEvent> CompleteScheduledEventAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.CalendarEdit);
        lock (_lock)
        {
            var scheduled = FindOpenEvent(id);
            if (scheduled.Type != ScheduledEventType.Other)
            {
                throw new ApiException(
                    HttpStatusCode.Conflict,
                    scheduled.Type == ScheduledEventType.Inbound
                        ? "Una descarga se cierra registrando su entrada."
                        : "Una carga se cierra registrando la salida de la mercancía.");
            }

            return Result(Replace(Close(scheduled, ScheduledEventStatus.Completed, user.Name, null)));
        }
    }

    public Task<ScheduledEvent> ReceiveScheduledEventAsync(Guid id, ReceiptInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            var scheduled = FindOpenEvent(id);
            if (scheduled.Type != ScheduledEventType.Inbound)
            {
                throw new ApiException(HttpStatusCode.Conflict, "Solo se registra una entrada desde una descarga.");
            }

            if (string.IsNullOrWhiteSpace(input.Reference))
            {
                input.Reference = Truncate(scheduled.Partner is null ? scheduled.Title : $"{scheduled.Title} · {scheduled.Partner}", 120);
            }

            var receipt = Receive(input, user.Name, _time.GetUtcNow());
            return Result(Replace(Close(scheduled, ScheduledEventStatus.Completed, user.Name, receipt.Code)));
        }
    }

    public Task<ScheduledEvent> ShipScheduledEventAsync(Guid id, ShipEventInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            var scheduled = FindOpenEvent(id);
            if (scheduled.Type != ScheduledEventType.Outbound)
            {
                throw new ApiException(HttpStatusCode.Conflict, "Solo se registra una salida desde una carga.");
            }

            var lines = input.Lines.Where(l => l.Quantity != 0 || l.ProductId is not null).ToList();
            if (lines.Count == 0)
            {
                throw Invalid(nameof(ShipEventInput.Lines), "Añade al menos un artículo.");
            }

            var errors = new List<string>();
            var shipments = new List<MovementInput>();
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.ProductId is null)
                {
                    errors.Add($"Línea {i + 1}: elige el artículo.");
                }

                if (line.FromLocationId is null)
                {
                    errors.Add($"Línea {i + 1}: elige de qué ubicación sale.");
                }

                if (line.Quantity <= 0)
                {
                    errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
                }

                shipments.Add(new MovementInput
                {
                    Type = MovementType.Shipment,
                    ProductId = line.ProductId,
                    FromLocationId = line.FromLocationId,
                    Lot = line.Lot,
                    Quantity = line.Quantity,
                    Reason = Truncate(scheduled.Partner ?? scheduled.Title, 200),
                });
            }

            if (errors.Count > 0)
            {
                throw new ApiException(
                    HttpStatusCode.BadRequest, "Revisa las líneas de la carga.", new Dictionary<string, string[]> { [nameof(ShipEventInput.Lines)] = [.. errors] });
            }

            // All or nothing, like the API's single save: check every line against the stock first.
            foreach (var group in shipments.GroupBy(s => (s.ProductId, s.FromLocationId, Lot: Normalize(s.Lot))))
            {
                var product = FindProduct(group.Key.ProductId!.Value);
                var from = FindLocation(group.Key.FromLocationId!.Value);
                var lot = product.TracksLots ? group.Key.Lot : null;
                if (product.TracksLots && lot is null)
                {
                    throw Invalid(nameof(ShipLineInput.Lot), $"{product.Sku} se controla por lotes: indica el lote.");
                }

                var available = _stock.FirstOrDefault(s => s.ProductId == product.Id && s.LocationId == from.Id && s.Lot == lot)?.Quantity ?? 0;
                var wanted = group.Sum(s => s.Quantity);
                if (available < wanted)
                {
                    throw new ApiException(
                        HttpStatusCode.Conflict, $"No hay stock suficiente de {product.Sku} en {from.Code}: hay {available:0.##} y se piden {wanted:0.##}.");
                }
            }

            var reference = string.IsNullOrWhiteSpace(input.Reference) ? Truncate(scheduled.Title, 40) : input.Reference.Trim();
            var now = _time.GetUtcNow();
            foreach (var shipment in shipments)
            {
                Apply(shipment, user.Name, now, reference);
            }

            return Result(Replace(Close(scheduled, ScheduledEventStatus.Completed, user.Name, reference)));
        }
    }

    private List<ScheduledEventLine> ValidateEvent(ScheduledEventInput input, bool isNew)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors[nameof(ScheduledEventInput.Title)] = ["Escribe un título."];
        }

        if (input.StartsAt == default)
        {
            errors[nameof(ScheduledEventInput.StartsAt)] = ["Indica cuándo empieza."];
        }
        else if (input.EndsAt <= input.StartsAt)
        {
            errors[nameof(ScheduledEventInput.EndsAt)] = ["Tiene que terminar después de empezar."];
        }
        else if (input.EndsAt - input.StartsAt > LongestEvent)
        {
            errors[nameof(ScheduledEventInput.EndsAt)] = ["No puede durar más de una semana."];
        }
        else if (isNew && input.EndsAt < _time.GetUtcNow())
        {
            errors[nameof(ScheduledEventInput.StartsAt)] = ["Esa fecha ya ha pasado."];
        }

        if (input.RepeatWeeks is < 0 or > 26)
        {
            errors[nameof(ScheduledEventInput.RepeatWeeks)] = ["Se puede repetir como mucho 26 semanas."];
        }

        if (input.DockLocationId is { } dockId)
        {
            var dock = _locations.FirstOrDefault(l => l.Id == dockId);
            var expected = input.Type switch
            {
                ScheduledEventType.Inbound => LocationType.Receiving,
                ScheduledEventType.Outbound => LocationType.Shipping,
                _ => (LocationType?)null,
            };
            if (dock is null)
            {
                errors[nameof(ScheduledEventInput.DockLocationId)] = ["La ubicación no existe."];
            }
            else if (expected is { } type && dock.Type != type)
            {
                errors[nameof(ScheduledEventInput.DockLocationId)] = [type == LocationType.Receiving
                    ? "Una descarga se reserva en una ubicación de recepción."
                    : "Una carga se reserva en una ubicación de expedición."];
            }
        }

        var lines = new List<ScheduledEventLine>();
        if (input.Type != ScheduledEventType.Other)
        {
            var lineErrors = new List<string>();
            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                var product = line.ProductId is { } productId ? _products.FirstOrDefault(p => p.Id == productId) : null;
                if (product is null)
                {
                    lineErrors.Add($"Línea {i + 1}: el artículo no existe.");
                    continue;
                }

                if (line.Quantity <= 0)
                {
                    lineErrors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
                    continue;
                }

                var same = lines.FindIndex(l => l.ProductId == product.Id);
                if (same >= 0)
                {
                    lines[same] = lines[same] with { Quantity = lines[same].Quantity + line.Quantity };
                }
                else
                {
                    lines.Add(new ScheduledEventLine(product.Id, product.Sku, product.Name, product.Unit, product.TracksLots, line.Quantity));
                }
            }

            if (lineErrors.Count > 0)
            {
                errors[nameof(ScheduledEventInput.Lines)] = [.. lineErrors];
            }
        }

        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Revisa los datos de la cita.", errors);
        }

        return [.. lines.OrderBy(l => l.Sku, StringComparer.Ordinal)];
    }

    private ScheduledEvent ToEvent(Guid id, ScheduledEventInput input, List<ScheduledEventLine> lines, TimeSpan shift) => new(
        id,
        input.Type,
        ScheduledEventStatus.Scheduled,
        input.Title.Trim(),
        string.IsNullOrWhiteSpace(input.Partner) ? null : input.Partner.Trim(),
        input.StartsAt + shift,
        input.EndsAt + shift,
        input.DockLocationId,
        input.DockLocationId is { } d ? _locations.First(l => l.Id == d).Code : null,
        string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
        null,
        "",
        default,
        null,
        null,
        null,
        lines);

    private void EnsureDockFree(ScheduledEvent scheduled)
    {
        if (scheduled.DockLocationId is not { } dockId)
        {
            return;
        }

        var clash = _events
            .Where(e => e.Id != scheduled.Id
                && e.DockLocationId == dockId
                && e.Status == ScheduledEventStatus.Scheduled
                && e.StartsAt < scheduled.EndsAt
                && e.EndsAt > scheduled.StartsAt)
            .OrderBy(e => e.StartsAt)
            .FirstOrDefault();
        if (clash is not null)
        {
            throw new ApiException(
                HttpStatusCode.Conflict, $"{scheduled.DockLocationCode} ya está reservado a esa hora para «{clash.Title}». Puedes programarlo igualmente.");
        }
    }

    private ScheduledEvent FindEvent(Guid id) =>
        _events.FirstOrDefault(e => e.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "La cita no existe.");

    private ScheduledEvent FindOpenEvent(Guid id)
    {
        var scheduled = FindEvent(id);
        return scheduled.Status == ScheduledEventStatus.Scheduled
            ? scheduled
            : throw new ApiException(
                HttpStatusCode.Conflict,
                scheduled.Status == ScheduledEventStatus.Completed ? "Esta cita ya está hecha." : "Esta cita está anulada.");
    }

    private ScheduledEvent Replace(ScheduledEvent scheduled)
    {
        _events[_events.FindIndex(e => e.Id == scheduled.Id)] = scheduled;
        return scheduled;
    }

    private ScheduledEvent Close(ScheduledEvent scheduled, ScheduledEventStatus status, string userName, string? outcome) =>
        scheduled with { Status = status, CompletedByName = userName, CompletedAt = _time.GetUtcNow(), Outcome = outcome };

    private static string? Normalize(string? lot) => string.IsNullOrWhiteSpace(lot) ? null : lot.Trim();

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length].TrimEnd();

    // The same week of dock appointments as the API's seed.
    private void SeedCalendar()
    {
        var now = _time.GetUtcNow();
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        // On the hour, so the slot ENTRADA2 came from reads like a real booking.
        var hour = today.AddHours(now.UtcDateTime.Hour);
        var weekly = Guid.Parse("6d3e0f50-0000-4000-8000-000000000001");
        _events.Add(Slot(1, ScheduledEventType.Inbound, "Descarga semanal", "Suministros Norte", hour.AddHours(-3), hour.AddHours(-2), 1, "Marta Gil",
            [(3, 20), (6, 10), (5, 10)], weekly) with
        {
            Status = ScheduledEventStatus.Completed,
            CompletedByName = "Marta Gil",
            CompletedAt = now.AddHours(-3),
            Outcome = "ENTRADA2",
        });
        _events.Add(Slot(2, ScheduledEventType.Outbound, "Pedido cliente 10240", "Hostelería Centro", today.AddHours(15), today.AddHours(16), 2, "Marta Gil", [(2, 20), (4, 6)]));
        _events.Add(Slot(3, ScheduledEventType.Inbound, "Aceite campaña nueva", "Almazara del Sur", today.AddDays(1).AddHours(7), today.AddDays(1).AddHours(8), 1, "Marta Gil", [(1, 96)]));
        _events.Add(Slot(4, ScheduledEventType.Outbound, "Ruta Getafe-Leganés", "Transportes Ruiz", today.AddDays(2).AddHours(12), today.AddDays(2).AddHours(13), 2, "Marta Gil", [(3, 10), (6, 4)]));
        _events.Add(Slot(5, ScheduledEventType.Other, "Recuento del pasillo A", null, today.AddDays(3).AddHours(6), today.AddDays(3).AddHours(9), null, "Lucía Martín", []));
        for (var week = 1; week <= 3; week++)
        {
            _events.Add(Slot(10 + week, ScheduledEventType.Inbound, "Descarga semanal", "Suministros Norte",
                hour.AddHours(-3).AddDays(7 * week), hour.AddHours(-2).AddDays(7 * week), 1, "Marta Gil", [(3, 20), (6, 10)], weekly));
        }
    }

    private ScheduledEvent Slot(
        int n, ScheduledEventType type, string title, string? partner, DateTimeOffset starts, DateTimeOffset ends, int? dock, string createdBy,
        (int Product, decimal Quantity)[] lines, Guid? series = null)
    {
        var dockLocation = dock is { } d ? _locations.First(l => l.Id == DemoData.LocationId(d)) : null;
        return new(
            Guid.Parse($"6d3e0f50-0000-4000-8000-1000000000{n:D2}"),
            type,
            ScheduledEventStatus.Scheduled,
            title,
            partner,
            starts,
            ends,
            dockLocation?.Id,
            dockLocation?.Code,
            null,
            series,
            createdBy,
            starts.AddDays(-5),
            null,
            null,
            null,
            [.. lines
                .Select(l => _products.First(p => p.Id == DemoData.ProductId(l.Product)))
                .Zip(lines)
                .Select(x => new ScheduledEventLine(x.First.Id, x.First.Sku, x.First.Name, x.First.Unit, x.First.TracksLots, x.Second.Quantity))
                .OrderBy(l => l.Sku, StringComparer.Ordinal)]);
    }
}
