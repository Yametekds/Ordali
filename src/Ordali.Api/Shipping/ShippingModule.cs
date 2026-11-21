using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Shipping;

public static class ShippingModule
{
    /// <summary>Shipments of a load are stamped with the time it was closed; this absorbs clock drift between the two.</summary>
    private static readonly TimeSpan ShipmentWindow = TimeSpan.FromMinutes(1);

    private const int MaxPallets = 99;

    public static void MapShippingEndpoints(this IEndpointRouteBuilder api)
    {
        var notes = api.MapGroup("/delivery-notes").WithTags("Albaranes");
        notes.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.MovementsView)
            .WithSummary("Albaranes de salida, del más reciente al más antiguo. 'eventId' busca el de una carga del calendario.");
        notes.MapGet("/draft", DraftAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .WithSummary("Propuesta de albarán para una carga ya registrada: cliente, referencia y lo que salió con sus lotes, todo en el palet 1.");
        notes.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Permissions.MovementsView)
            .WithName("GetDeliveryNote");
        notes.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<DeliveryNoteInput>()
            .WithSummary("Crea el albarán. Cada línea indica en qué palet va; los números se compactan a 1, 2, 3… No mueve stock.");
    }

    private static async Task<IResult> ListAsync(Guid? eventId, OrdaliDbContext db, CancellationToken cancellationToken, int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var notes = await db.DeliveryNotes.AsNoTracking().Include(n => n.Lines)
            .Where(n => eventId == null || n.ScheduledEventId == eventId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(db, notes, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var note = await db.DeliveryNotes.AsNoTracking().Include(n => n.Lines).FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        return note is null ? NotFound() : Results.Ok((await ToDtosAsync(db, [note], cancellationToken))[0]);
    }

    private static async Task<IResult> DraftAsync(Guid eventId, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var scheduled = await db.ScheduledEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
        if (scheduled is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "La cita no existe.");
        }

        if (ShippedProblem(scheduled) is { } problem)
        {
            return problem;
        }

        var existing = await db.DeliveryNotes.AsNoTracking()
            .Where(n => n.ScheduledEventId == eventId)
            .Select(n => (Guid?)n.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var shippedAt = scheduled.CompletedAt!.Value;
        var from = shippedAt - ShipmentWindow;
        var until = shippedAt + ShipmentWindow;
        var shipments = await db.StockMovements.AsNoTracking()
            .Where(m => m.Type == MovementType.Shipment && m.Reference == scheduled.Outcome && m.CreatedAt >= from && m.CreatedAt <= until)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var productIds = shipments.Select(m => m.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        // The same item and lot picked from two locations is one line on paper.
        var lines = shipments
            .GroupBy(m => (m.ProductId, m.Lot))
            .Select(g =>
            {
                var product = products[g.Key.ProductId];
                return new DeliveryNoteLineDto(product.Id, product.Sku, product.Name, product.Unit, product.Gtin, g.Key.Lot, g.Sum(m => m.Quantity), 1);
            })
            .ToList();

        return Results.Ok(new DeliveryNoteDraftDto(
            scheduled.Id, existing, scheduled.Partner ?? scheduled.Title, scheduled.Outcome, shippedAt, lines));
    }

    private static async Task<IResult> CreateAsync(
        DeliveryNoteInput input, ClaimsPrincipal user, OrdaliDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        if (input.ScheduledEventId is { } eventId)
        {
            var scheduled = await db.ScheduledEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            if (scheduled is null)
            {
                return Validation.Invalid(nameof(DeliveryNoteInput.ScheduledEventId), "La cita no existe.");
            }

            if (ShippedProblem(scheduled) is { } problem)
            {
                return problem;
            }

            if (await db.DeliveryNotes.AnyAsync(n => n.ScheduledEventId == eventId, cancellationToken))
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "Esta carga ya tiene su albarán.");
            }
        }

        if (input.Lines.Count == 0)
        {
            return Validation.Invalid(nameof(DeliveryNoteInput.Lines), "Añade al menos un artículo.");
        }

        var productIds = input.Lines.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var errors = new List<string>();
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            if (line.ProductId is not { } productId || !products.TryGetValue(productId, out var product))
            {
                errors.Add($"Línea {i + 1}: el artículo no existe.");
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
            }

            if (line.Pallet is < 1 or > MaxPallets)
            {
                errors.Add($"Línea {i + 1}: el palet tiene que ir del 1 al {MaxPallets}.");
            }

            if (line.Lot?.Trim().Length > 40)
            {
                errors.Add($"Línea {i + 1}: el lote no puede pasar de 40 caracteres.");
            }
            else if (product.TracksLots && string.IsNullOrWhiteSpace(line.Lot))
            {
                errors.Add($"Línea {i + 1}: {product.Sku} se controla por lotes, indica el lote.");
            }
        }

        if (errors.Count > 0)
        {
            return Validation.Invalid(new Dictionary<string, string[]> { ["lines"] = [.. errors] });
        }

        // Pallets 1, 3 and 7 print as 1, 2 and 3 of 3.
        var pallets = PalletNumbers(input.Lines.Select(l => l.Pallet));
        var note = new DeliveryNote
        {
            Id = Guid.NewGuid(),
            Code = await NextCodeAsync(db, cancellationToken),
            ScheduledEventId = input.ScheduledEventId,
            Customer = input.Customer.Trim(),
            Address = Clean(input.Address),
            Carrier = Clean(input.Carrier),
            VehiclePlate = Clean(input.VehiclePlate)?.ToUpperInvariant(),
            Reference = Clean(input.Reference),
            Notes = Clean(input.Notes),
            UserId = user.UserId(),
            UserName = user.Identity!.Name!,
            CreatedAt = time.GetUtcNow(),
        };

        var position = 0;
        foreach (var line in input.Lines.OrderBy(l => pallets[l.Pallet]))
        {
            var lot = Clean(line.Lot);
            var pallet = pallets[line.Pallet];
            if (note.Lines.FirstOrDefault(l => l.ProductId == line.ProductId && l.Lot == lot && l.Pallet == pallet) is { } same)
            {
                same.Quantity += line.Quantity;
                continue;
            }

            note.Lines.Add(new DeliveryNoteLine
            {
                DeliveryNoteId = note.Id,
                ProductId = line.ProductId!.Value,
                Lot = lot,
                Quantity = line.Quantity,
                Pallet = pallet,
                Position = position++,
            });
        }

        db.DeliveryNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        var dto = (await ToDtosAsync(db, [note], cancellationToken))[0];
        return Results.CreatedAtRoute("GetDeliveryNote", new { id = note.Id }, dto);
    }

    /// <summary>Maps the pallet numbers used to 1..n, keeping their order.</summary>
    internal static Dictionary<int, int> PalletNumbers(IEnumerable<int> used) =>
        used.Distinct().Order().Select((number, index) => (number, index)).ToDictionary(x => x.number, x => x.index + 1);

    private static IResult? ShippedProblem(ScheduledEvent scheduled)
    {
        if (scheduled.Type != ScheduledEventType.Outbound)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "Solo una carga lleva albarán.");
        }

        if (scheduled.Status != ScheduledEventStatus.Completed || scheduled.CompletedAt is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: "Registra primero la carga; el albarán sale de lo que se ha cargado.");
        }

        return null;
    }

    private static async Task<string> NextCodeAsync(OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var codes = await db.DeliveryNotes.AsNoTracking().Select(n => n.Code).ToListAsync(cancellationToken);
        var last = codes
            .Select(c => c.StartsWith("ALB-", StringComparison.Ordinal) && int.TryParse(c.AsSpan(4), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"ALB-{last + 1:D4}";
    }

    private static async Task<List<DeliveryNoteDto>> ToDtosAsync(OrdaliDbContext db, IReadOnlyList<DeliveryNote> notes, CancellationToken cancellationToken)
    {
        var productIds = notes.SelectMany(n => n.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return [.. notes.Select(n => new DeliveryNoteDto(
            n.Id, n.Code, n.ScheduledEventId, n.Customer, n.Address, n.Carrier, n.VehiclePlate, n.Reference, n.Notes, n.UserName, n.CreatedAt,
            n.Lines.Select(l => l.Pallet).DefaultIfEmpty(0).Max(),
            [.. n.Lines.OrderBy(l => l.Pallet).ThenBy(l => l.Position).Select(l =>
            {
                var product = products[l.ProductId];
                return new DeliveryNoteLineDto(product.Id, product.Sku, product.Name, product.Unit, product.Gtin, l.Lot, l.Quantity, l.Pallet);
            })]))];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "El albarán no existe.");
}
