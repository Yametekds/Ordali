using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Inventory;

namespace Ordali.Api.Calendar;

/// <summary>
/// Loading and unloading slots and other calendar entries. An unloading slot is closed by
/// recording its receipt, a loading slot by shipping its goods, and anything else by marking it done.
/// </summary>
public sealed class CalendarService(OrdaliDbContext db, MovementService movements, ReceiptService receipts, TimeProvider time)
{
    private static readonly TimeSpan LongestEvent = TimeSpan.FromDays(7);

    public async Task<List<ScheduledEvent>> CreateAsync(ScheduledEventInput input, string userName, CancellationToken cancellationToken)
    {
        var lines = await ValidateAsync(input, null, cancellationToken);
        var seriesId = input.RepeatWeeks > 0 ? Guid.NewGuid() : (Guid?)null;
        var now = time.GetUtcNow();
        var created = new List<ScheduledEvent>();
        for (var week = 0; week <= input.RepeatWeeks; week++)
        {
            var scheduled = new ScheduledEvent
            {
                Id = Guid.NewGuid(),
                Title = "",
                SeriesId = seriesId,
                Status = ScheduledEventStatus.Scheduled,
                CreatedByName = userName,
                CreatedAt = now,
            };
            Apply(scheduled, input, lines, TimeSpan.FromDays(7 * week));
            created.Add(scheduled);
        }

        if (!input.AllowOverlap)
        {
            foreach (var scheduled in created)
            {
                await EnsureDockFreeAsync(scheduled, cancellationToken);
            }
        }

        db.ScheduledEvents.AddRange(created);
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<ScheduledEvent> UpdateAsync(Guid id, ScheduledEventInput input, CancellationToken cancellationToken)
    {
        var scheduled = await FindOpenAsync(id, cancellationToken);
        var lines = await ValidateAsync(input, scheduled, cancellationToken);
        db.RemoveRange(scheduled.Lines);
        scheduled.Lines.Clear();
        Apply(scheduled, input, lines, TimeSpan.Zero);

        // The lines come with their keys set, so EF would otherwise take them for existing rows.
        db.AddRange(scheduled.Lines);
        if (!input.AllowOverlap)
        {
            await EnsureDockFreeAsync(scheduled, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return scheduled;
    }

    public async Task<ScheduledEvent> CancelAsync(Guid id, string userName, CancellationToken cancellationToken)
    {
        var scheduled = await FindOpenAsync(id, cancellationToken);
        Close(scheduled, ScheduledEventStatus.Cancelled, userName, null);
        await db.SaveChangesAsync(cancellationToken);
        return scheduled;
    }

    /// <summary>Closes an entry that doesn't move stock, such as a stock count.</summary>
    public async Task<ScheduledEvent> CompleteAsync(Guid id, string userName, CancellationToken cancellationToken)
    {
        var scheduled = await FindOpenAsync(id, cancellationToken);
        if (scheduled.Type != ScheduledEventType.Other)
        {
            throw new MovementRejectedException(
                StatusCodes.Status409Conflict,
                scheduled.Type == ScheduledEventType.Inbound
                    ? "Una descarga se cierra registrando su entrada."
                    : "Una carga se cierra registrando la salida de la mercancía.");
        }

        Close(scheduled, ScheduledEventStatus.Completed, userName, null);
        await db.SaveChangesAsync(cancellationToken);
        return scheduled;
    }

    /// <summary>Records the receipt of an unloading slot; the goods wait on the dock to be put away as usual.</summary>
    public async Task<(ScheduledEvent Event, Receipt Receipt)> ReceiveAsync(
        Guid id, ReceiptInput input, Guid userId, string userName, CancellationToken cancellationToken)
    {
        var scheduled = await FindOpenAsync(id, cancellationToken);
        if (scheduled.Type != ScheduledEventType.Inbound)
        {
            throw new MovementRejectedException(StatusCodes.Status409Conflict, "Solo se registra una entrada desde una descarga.");
        }

        if (string.IsNullOrWhiteSpace(input.Reference))
        {
            input.Reference = Truncate(scheduled.Partner is null ? scheduled.Title : $"{scheduled.Title} · {scheduled.Partner}", 120);
        }

        // The receipt saves the event too, so both change together or not at all.
        var now = time.GetUtcNow();
        scheduled.Status = ScheduledEventStatus.Completed;
        scheduled.CompletedByName = userName;
        scheduled.CompletedAt = now;
        var receipt = await receipts.CreateAsync(input, userId, userName, cancellationToken, now, beforeSave: r => scheduled.Outcome = r.Code);
        return (scheduled, receipt);
    }

    /// <summary>Ships the goods of a loading slot, each line from the location it is picked from.</summary>
    public async Task<ScheduledEvent> ShipAsync(Guid id, ShipEventInput input, Guid userId, string userName, CancellationToken cancellationToken)
    {
        var scheduled = await FindOpenAsync(id, cancellationToken);
        if (scheduled.Type != ScheduledEventType.Outbound)
        {
            throw new MovementRejectedException(StatusCodes.Status409Conflict, "Solo se registra una salida desde una carga.");
        }

        var lines = input.Lines.Where(l => l.Quantity != 0 || l.ProductId is not null).ToList();
        if (lines.Count == 0)
        {
            throw Invalid("lines", "Añade al menos un artículo.");
        }

        var errors = new List<string>();
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
        }

        if (errors.Count > 0)
        {
            throw new MovementRejectedException(
                StatusCodes.Status400BadRequest, "Revisa las líneas de la carga.", new Dictionary<string, string[]> { ["lines"] = [.. errors] });
        }

        var reference = string.IsNullOrWhiteSpace(input.Reference) ? Truncate(scheduled.Title, 40) : input.Reference.Trim();
        var now = time.GetUtcNow();
        foreach (var line in lines)
        {
            var shipment = new MovementInput
            {
                Type = MovementType.Shipment,
                ProductId = line.ProductId,
                FromLocationId = line.FromLocationId,
                Lot = line.Lot,
                Quantity = line.Quantity,
                Reason = Truncate(scheduled.Partner ?? scheduled.Title, 200),
            };
            await movements.StageAsync(shipment, userId, userName, cancellationToken, now, reference);
        }

        Close(scheduled, ScheduledEventStatus.Completed, userName, reference);
        await movements.SaveAsync(cancellationToken);
        return scheduled;
    }

    private async Task<List<ScheduledEventLine>> ValidateAsync(ScheduledEventInput input, ScheduledEvent? existing, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            errors["title"] = ["Escribe un título."];
        }

        if (input.StartsAt == default)
        {
            errors["startsAt"] = ["Indica cuándo empieza."];
        }
        else if (input.EndsAt <= input.StartsAt)
        {
            errors["endsAt"] = ["Tiene que terminar después de empezar."];
        }
        else if (input.EndsAt - input.StartsAt > LongestEvent)
        {
            errors["endsAt"] = ["No puede durar más de una semana."];
        }
        else if (existing is null && input.EndsAt < time.GetUtcNow())
        {
            errors["startsAt"] = ["Esa fecha ya ha pasado."];
        }

        if (input.DockLocationId is { } dockId)
        {
            var dock = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.Id == dockId, cancellationToken);
            var expected = input.Type switch
            {
                ScheduledEventType.Inbound => LocationType.Receiving,
                ScheduledEventType.Outbound => LocationType.Shipping,
                _ => (LocationType?)null,
            };
            if (dock is null)
            {
                errors["dockLocationId"] = ["La ubicación no existe."];
            }
            else if (expected is { } type && dock.Type != type)
            {
                errors["dockLocationId"] = [type == LocationType.Receiving
                    ? "Una descarga se reserva en una ubicación de recepción."
                    : "Una carga se reserva en una ubicación de expedición."];
            }
        }

        var lines = new List<ScheduledEventLine>();
        if (input.Type != ScheduledEventType.Other)
        {
            var ids = input.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
            var known = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(cancellationToken);
            var lineErrors = new List<string>();
            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                if (line.ProductId is not { } productId || !known.Contains(productId))
                {
                    lineErrors.Add($"Línea {i + 1}: el artículo no existe.");
                    continue;
                }

                if (line.Quantity <= 0)
                {
                    lineErrors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
                    continue;
                }

                // The same item twice becomes one line.
                if (lines.FirstOrDefault(l => l.ProductId == productId) is { } same)
                {
                    same.Quantity += line.Quantity;
                }
                else
                {
                    lines.Add(new ScheduledEventLine { Id = Guid.NewGuid(), ProductId = productId, Quantity = line.Quantity });
                }
            }

            if (lineErrors.Count > 0)
            {
                errors["lines"] = [.. lineErrors];
            }
        }

        if (errors.Count > 0)
        {
            throw new MovementRejectedException(StatusCodes.Status400BadRequest, "Revisa los datos de la cita.", errors);
        }

        return lines;
    }

    private static void Apply(ScheduledEvent scheduled, ScheduledEventInput input, List<ScheduledEventLine> lines, TimeSpan shift)
    {
        scheduled.Type = input.Type;
        scheduled.Title = input.Title.Trim();
        scheduled.Partner = string.IsNullOrWhiteSpace(input.Partner) ? null : input.Partner.Trim();
        scheduled.StartsAt = input.StartsAt + shift;
        scheduled.EndsAt = input.EndsAt + shift;
        scheduled.DockLocationId = input.DockLocationId;
        scheduled.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        scheduled.Lines = [.. lines.Select(l => new ScheduledEventLine { Id = Guid.NewGuid(), ProductId = l.ProductId, Quantity = l.Quantity })];
    }

    private async Task EnsureDockFreeAsync(ScheduledEvent scheduled, CancellationToken cancellationToken)
    {
        if (scheduled.DockLocationId is not { } dockId)
        {
            return;
        }

        var clash = await db.ScheduledEvents.AsNoTracking()
            .Where(e => e.Id != scheduled.Id
                && e.DockLocationId == dockId
                && e.Status == ScheduledEventStatus.Scheduled
                && e.StartsAt < scheduled.EndsAt
                && e.EndsAt > scheduled.StartsAt)
            .OrderBy(e => e.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (clash is not null)
        {
            var dock = await db.Locations.AsNoTracking().Where(l => l.Id == dockId).Select(l => l.Code).FirstAsync(cancellationToken);
            throw new MovementRejectedException(
                StatusCodes.Status409Conflict,
                $"{dock} ya está reservado a esa hora para «{clash.Title}». Puedes programarlo igualmente.");
        }
    }

    private async Task<ScheduledEvent> FindOpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var scheduled = await db.ScheduledEvents.Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new MovementRejectedException(StatusCodes.Status404NotFound, "La cita no existe.");
        return scheduled.Status == ScheduledEventStatus.Scheduled
            ? scheduled
            : throw new MovementRejectedException(
                StatusCodes.Status409Conflict,
                scheduled.Status == ScheduledEventStatus.Completed ? "Esta cita ya está hecha." : "Esta cita está anulada.");
    }

    private void Close(ScheduledEvent scheduled, ScheduledEventStatus status, string userName, string? outcome)
    {
        scheduled.Status = status;
        scheduled.CompletedByName = userName;
        scheduled.CompletedAt = time.GetUtcNow();
        scheduled.Outcome = outcome;
    }

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..length].TrimEnd();

    private static MovementRejectedException Invalid(string field, string message) =>
        new(StatusCodes.Status400BadRequest, message, new Dictionary<string, string[]> { [field] = [message] });
}
