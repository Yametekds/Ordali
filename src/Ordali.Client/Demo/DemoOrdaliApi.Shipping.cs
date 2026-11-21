using System.Net;
using Ordali.Client.Models;

namespace Ordali.Client.Demo;

// Delivery notes of the demo mode, with the same rules as the API's shipping module.
public sealed partial class DemoOrdaliApi
{
    private const int MaxPallets = 99;
    private static readonly TimeSpan ShipmentWindow = TimeSpan.FromMinutes(1);
    private readonly List<DeliveryNote> _deliveryNotes = [];

    public Task<IReadOnlyList<DeliveryNote>> GetDeliveryNotesAsync(Guid? eventId = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsView);
        lock (_lock)
        {
            return Result<IReadOnlyList<DeliveryNote>>(
                [.. _deliveryNotes.Where(n => eventId is null || n.ScheduledEventId == eventId).OrderByDescending(n => n.CreatedAt)]);
        }
    }

    public Task<DeliveryNote> GetDeliveryNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsView);
        lock (_lock)
        {
            return Result(_deliveryNotes.FirstOrDefault(n => n.Id == id)
                ?? throw new ApiException(HttpStatusCode.NotFound, Text.Get("El albarán no existe.")));
        }
    }

    public Task<DeliveryNoteDraft> GetDeliveryNoteDraftAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            var scheduled = FindEvent(eventId);
            EnsureShipped(scheduled);
            var shippedAt = scheduled.CompletedAt!.Value;
            var lines = _movements
                .Where(m => m.Type == MovementType.Shipment && m.Reference == scheduled.Outcome
                    && m.CreatedAt >= shippedAt - ShipmentWindow && m.CreatedAt <= shippedAt + ShipmentWindow)
                .OrderBy(m => m.CreatedAt)
                .GroupBy(m => (m.ProductId, m.Lot))
                .Select(g =>
                {
                    var product = FindProduct(g.Key.ProductId);
                    return new DeliveryNoteLine(product.Id, product.Sku, product.Name, product.Unit, product.Gtin, g.Key.Lot, g.Sum(m => m.Quantity), 1);
                })
                .ToList();
            var existing = _deliveryNotes.FirstOrDefault(n => n.ScheduledEventId == eventId)?.Id;
            return Result(new DeliveryNoteDraft(scheduled.Id, existing, scheduled.Partner ?? scheduled.Title, scheduled.Outcome, shippedAt, lines));
        }
    }

    public Task<DeliveryNote> CreateDeliveryNoteAsync(DeliveryNoteInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.MovementsCreate);
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(input.Customer))
            {
                throw Invalid(nameof(DeliveryNoteInput.Customer), Text.Get("Indica el cliente."));
            }

            if (input.ScheduledEventId is { } eventId)
            {
                EnsureShipped(FindEvent(eventId));
                if (_deliveryNotes.Any(n => n.ScheduledEventId == eventId))
                {
                    throw new ApiException(HttpStatusCode.Conflict, Text.Get("Esta carga ya tiene su albarán."));
                }
            }

            if (input.Lines.Count == 0)
            {
                throw Invalid(nameof(DeliveryNoteInput.Lines), Text.Get("Añade al menos un artículo."));
            }

            var errors = new List<string>();
            for (var i = 0; i < input.Lines.Count; i++)
            {
                var line = input.Lines[i];
                var product = line.ProductId is { } productId ? _products.FirstOrDefault(p => p.Id == productId) : null;
                if (product is null)
                {
                    errors.Add(Text.Format("Línea {0}: el artículo no existe.", i + 1));
                    continue;
                }

                if (line.Quantity <= 0)
                {
                    errors.Add(Text.Format("Línea {0}: la cantidad tiene que ser mayor que cero.", i + 1));
                }

                if (line.Pallet is < 1 or > MaxPallets)
                {
                    errors.Add(Text.Format("Línea {0}: el palet tiene que ir del 1 al {1}.", i + 1, MaxPallets));
                }

                if (product.TracksLots && string.IsNullOrWhiteSpace(line.Lot))
                {
                    errors.Add(Text.Format("Línea {0}: {1} se controla por lotes, indica el lote.", i + 1, product.Sku));
                }
            }

            if (errors.Count > 0)
            {
                throw new ApiException(
                    HttpStatusCode.BadRequest, Text.Get("Revisa los datos enviados."), new Dictionary<string, string[]> { [nameof(DeliveryNoteInput.Lines)] = [.. errors] });
            }

            var pallets = input.Lines.Select(l => l.Pallet).Distinct().Order()
                .Select((number, index) => (number, index)).ToDictionary(x => x.number, x => x.index + 1);
            var lines = new List<DeliveryNoteLine>();
            foreach (var line in input.Lines.OrderBy(l => pallets[l.Pallet]))
            {
                var product = FindProduct(line.ProductId!.Value);
                var lot = Normalize(line.Lot);
                var pallet = pallets[line.Pallet];
                var index = lines.FindIndex(l => l.ProductId == product.Id && l.Lot == lot && l.Pallet == pallet);
                if (index >= 0)
                {
                    lines[index] = lines[index] with { Quantity = lines[index].Quantity + line.Quantity };
                    continue;
                }

                lines.Add(new DeliveryNoteLine(product.Id, product.Sku, product.Name, product.Unit, product.Gtin, lot, line.Quantity, pallet));
            }

            var last = _deliveryNotes
                .Select(n => int.TryParse(n.Code.AsSpan(4), out var number) ? number : 0)
                .DefaultIfEmpty(0)
                .Max();
            var note = new DeliveryNote(
                Guid.NewGuid(),
                $"ALB-{last + 1:D4}",
                input.ScheduledEventId,
                input.Customer.Trim(),
                Clean(input.Address),
                Clean(input.Carrier),
                Clean(input.VehiclePlate)?.ToUpperInvariant(),
                Clean(input.Reference),
                Clean(input.Notes),
                user.Name,
                _time.GetUtcNow(),
                pallets.Count,
                lines);
            _deliveryNotes.Add(note);
            return Result(note);
        }
    }

    private static void EnsureShipped(ScheduledEvent scheduled)
    {
        if (scheduled.Type != ScheduledEventType.Outbound)
        {
            throw new ApiException(HttpStatusCode.Conflict, Text.Get("Solo una carga lleva albarán."));
        }

        if (scheduled.Status != ScheduledEventStatus.Completed || scheduled.CompletedAt is null)
        {
            throw new ApiException(HttpStatusCode.Conflict, Text.Get("Registra primero la carga; el albarán sale de lo que se ha cargado."));
        }
    }

    // The paperwork of the seeded shipment of order 10233.
    private void SeedDeliveryNotes()
    {
        var screws = _products.First(p => p.Sku == "TOR-M8");
        _deliveryNotes.Add(new DeliveryNote(
            Guid.NewGuid(), "ALB-0001", null, "Ferretería Getafe Centro", "Av. de España 12, 28901 Getafe (Madrid)", "Reparto propio", null, "10233", null,
            "Javier Ruiz", _time.GetUtcNow().AddHours(-5), 1,
            [new DeliveryNoteLine(screws.Id, screws.Sku, screws.Name, screws.Unit, screws.Gtin, null, 4, 1)]));
    }
}
