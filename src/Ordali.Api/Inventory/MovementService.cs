using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Inventory;

/// <summary>A movement that can't be applied, with the HTTP status and the message for the user.</summary>
public sealed class MovementRejectedException(int status, string message, IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int Status { get; } = status;

    public IDictionary<string, string[]>? Errors { get; } = errors;
}

/// <summary>Applies a movement to the stock and writes it to the log in the same save.</summary>
public sealed class MovementService(OrdaliDbContext db, TimeProvider time)
{
    public async Task<StockMovement> RecordAsync(MovementInput input, Guid userId, string userName, CancellationToken cancellationToken, DateTimeOffset? at = null)
    {
        var movement = await StageAsync(input, userId, userName, cancellationToken, at);
        await SaveAsync(cancellationToken);
        return movement;
    }

    /// <summary>
    /// Applies the movement to the tracked stock lines without saving, so a receipt or a
    /// put-away can save several changes together.
    /// </summary>
    public async Task<StockMovement> StageAsync(
        MovementInput input, Guid userId, string userName, CancellationToken cancellationToken, DateTimeOffset? at = null, string? reference = null)
    {
        var errors = MovementRules.Validate(input);
        if (errors.Count > 0)
        {
            throw new MovementRejectedException(StatusCodes.Status400BadRequest, "Revisa los datos del movimiento.", errors);
        }

        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == input.ProductId, cancellationToken)
            ?? throw new MovementRejectedException(StatusCodes.Status404NotFound, "El artículo no existe.");

        var lot = string.IsNullOrWhiteSpace(input.Lot) ? null : input.Lot.Trim();
        if (product.TracksLots && lot is null)
        {
            throw new MovementRejectedException(
                StatusCodes.Status400BadRequest,
                "Este artículo se controla por lotes: indica el lote.",
                new Dictionary<string, string[]> { ["lot"] = ["Este artículo se controla por lotes: indica el lote."] });
        }

        if (!product.TracksLots)
        {
            lot = null;
        }

        var from = input.FromLocationId is { } fromId ? await FindLocationAsync(fromId, cancellationToken) : null;
        var to = input.ToLocationId is { } toId ? await FindLocationAsync(toId, cancellationToken) : null;

        switch (input.Type)
        {
            case MovementType.Receipt:
                (await LineAsync(product.Id, to!.Id, lot, cancellationToken)).Quantity += input.Quantity;
                break;
            case MovementType.Shipment:
                await TakeAsync(product, from!, lot, input.Quantity, cancellationToken);
                break;
            case MovementType.Transfer:
                await TakeAsync(product, from!, lot, input.Quantity, cancellationToken);
                (await LineAsync(product.Id, to!.Id, lot, cancellationToken)).Quantity += input.Quantity;
                break;
            case MovementType.Adjustment:
                var line = await LineAsync(product.Id, to!.Id, lot, cancellationToken);
                if (line.Quantity + input.Quantity < 0)
                {
                    throw new MovementRejectedException(
                        StatusCodes.Status409Conflict, $"El ajuste dejaría el stock en negativo (hay {line.Quantity:0.##}).");
                }

                line.Quantity += input.Quantity;
                break;
            default:
                throw new MovementRejectedException(StatusCodes.Status400BadRequest, "Tipo de movimiento desconocido.");
        }

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            Type = input.Type,
            ProductId = product.Id,
            Lot = lot,
            FromLocationId = from?.Id,
            ToLocationId = to?.Id,
            Quantity = input.Quantity,
            Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim(),
            Reference = reference,
            UserId = userId,
            UserName = userName,
            CreatedAt = at ?? time.GetUtcNow(),
        };
        db.StockMovements.Add(movement);
        return movement;
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Someone else changed the same stock line between our read and our write
            // (stale Version, or both created the same line).
            db.ChangeTracker.Clear();
            throw new MovementRejectedException(
                StatusCodes.Status409Conflict, "El stock ha cambiado mientras se guardaba el movimiento. Vuelve a intentarlo.");
        }
    }

    private async Task<Location> FindLocationAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Locations.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
        ?? throw new MovementRejectedException(StatusCodes.Status404NotFound, "La ubicación no existe.");

    private async Task TakeAsync(Product product, Location from, string? lot, decimal quantity, CancellationToken cancellationToken)
    {
        var line = await FindLineAsync(product.Id, from.Id, lot, cancellationToken);
        var available = line?.Quantity ?? 0;
        if (available < quantity)
        {
            throw new MovementRejectedException(
                StatusCodes.Status409Conflict,
                $"No hay stock suficiente de {product.Sku} en {from.Code}: hay {available:0.##} y se piden {quantity:0.##}.");
        }

        line!.Quantity -= quantity;
    }

    private async Task<StockLine> LineAsync(Guid productId, Guid locationId, string? lot, CancellationToken cancellationToken)
    {
        var line = await FindLineAsync(productId, locationId, lot, cancellationToken);
        if (line is null)
        {
            line = new StockLine { Id = Guid.NewGuid(), ProductId = productId, LocationId = locationId, Lot = lot };
            db.StockLines.Add(line);
        }

        return line;
    }

    private async Task<StockLine?> FindLineAsync(Guid productId, Guid locationId, string? lot, CancellationToken cancellationToken) =>
        db.StockLines.Local.FirstOrDefault(s => s.ProductId == productId && s.LocationId == locationId && s.Lot == lot)
        ?? await db.StockLines.FirstOrDefaultAsync(s => s.ProductId == productId && s.LocationId == locationId && s.Lot == lot, cancellationToken);
}
