using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Inventory;

/// <summary>
/// Receipts in two steps: recording one brings the goods into the receiving location, and each
/// put-away moves part of a line from there to its final location.
/// </summary>
public sealed class ReceiptService(OrdaliDbContext db, MovementService movements, TimeProvider time)
{
    public async Task<Receipt> CreateAsync(ReceiptInput input, Guid userId, string userName, CancellationToken cancellationToken, DateTimeOffset? at = null)
    {
        var lines = await ValidateLinesAsync(input.Lines, cancellationToken);

        var code = string.IsNullOrWhiteSpace(input.Code) ? await NextCodeAsync(cancellationToken) : Normalize(input.Code);
        if (await db.Receipts.AnyAsync(r => r.Code == code, cancellationToken))
        {
            throw Invalid("code", $"Ya hay una entrada con el código {code}.");
        }

        var dock = await db.Locations
            .Where(l => l.Type == LocationType.Receiving)
            .OrderBy(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new MovementRejectedException(
                StatusCodes.Status409Conflict, "No hay ninguna ubicación de recepción donde dejar la mercancía. Crea una de tipo Recepción.");

        var now = at ?? time.GetUtcNow();
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            Code = code,
            Reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim(),
            DockLocationId = dock.Id,
            UserId = userId,
            UserName = userName,
            CreatedAt = now,
        };

        foreach (var line in lines)
        {
            var movement = new MovementInput
            {
                Type = MovementType.Receipt,
                ProductId = line.ProductId,
                ToLocationId = dock.Id,
                Lot = line.Lot,
                Quantity = line.Quantity,
                Reason = receipt.Reference,
            };
            await movements.StageAsync(movement, userId, userName, cancellationToken, now, code);
            receipt.Lines.Add(new ReceiptLine { Id = Guid.NewGuid(), ProductId = line.ProductId, Lot = line.Lot, Quantity = line.Quantity });
        }

        db.Receipts.Add(receipt);
        await movements.SaveAsync(cancellationToken);
        return receipt;
    }

    public async Task<Receipt> PutAwayAsync(Guid receiptId, PutAwayInput input, Guid userId, string userName, CancellationToken cancellationToken, DateTimeOffset? at = null)
    {
        var receipt = await db.Receipts.Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken)
            ?? throw new MovementRejectedException(StatusCodes.Status404NotFound, "La entrada no existe.");

        var errors = new Dictionary<string, string[]>();
        if (input.ProductId is null)
        {
            errors["productId"] = ["Escanea o elige el artículo."];
        }

        if (input.ToLocationId is null)
        {
            errors["toLocationId"] = ["Escanea o elige la ubicación de destino."];
        }
        else if (input.ToLocationId == receipt.DockLocationId)
        {
            errors["toLocationId"] = ["El destino tiene que ser distinto del muelle de recepción."];
        }

        if (input.Quantity <= 0)
        {
            errors["quantity"] = ["Escribe la cantidad que mueves."];
        }

        if (errors.Count > 0)
        {
            throw new MovementRejectedException(StatusCodes.Status400BadRequest, "Revisa los datos del traslado.", errors);
        }

        var lot = string.IsNullOrWhiteSpace(input.Lot) ? null : input.Lot.Trim();
        var candidates = receipt.Lines.Where(l => l.ProductId == input.ProductId).ToList();
        if (candidates.Count == 0)
        {
            throw Invalid("productId", $"Ese artículo no está en la entrada {receipt.Code}.");
        }

        // The lot can be left out when the item came in a single lot.
        var line = lot is null && candidates.Count == 1
            ? candidates[0]
            : candidates.FirstOrDefault(l => string.Equals(l.Lot, lot, StringComparison.OrdinalIgnoreCase))
              ?? throw Invalid("lot", lot is null ? "Indica el lote." : $"El lote {lot} no está en la entrada {receipt.Code}.");

        var pending = line.Quantity - line.PutAwayQuantity;
        if (pending <= 0)
        {
            throw new MovementRejectedException(StatusCodes.Status409Conflict, "Este artículo ya está ubicado del todo.");
        }

        if (input.Quantity > pending)
        {
            throw Invalid("quantity", $"Solo quedan {pending:0.##} por ubicar.");
        }

        var now = at ?? time.GetUtcNow();
        var transfer = new MovementInput
        {
            Type = MovementType.Transfer,
            ProductId = line.ProductId,
            FromLocationId = receipt.DockLocationId,
            ToLocationId = input.ToLocationId,
            Lot = line.Lot,
            Quantity = input.Quantity,
        };
        await movements.StageAsync(transfer, userId, userName, cancellationToken, now, receipt.Code);

        line.PutAwayQuantity += input.Quantity;
        if (receipt.Lines.All(l => l.PutAwayQuantity >= l.Quantity))
        {
            receipt.CompletedAt = now;
        }

        await movements.SaveAsync(cancellationToken);
        return receipt;
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private async Task<List<(Guid ProductId, string? Lot, decimal Quantity)>> ValidateLinesAsync(
        List<ReceiptLineInput> input, CancellationToken cancellationToken)
    {
        if (input.Count == 0)
        {
            throw Invalid("lines", "Añade al menos un artículo.");
        }

        var ids = input.Where(l => l.ProductId is not null).Select(l => l.ProductId!.Value).Distinct().ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var errors = new List<string>();
        var lines = new List<(Guid ProductId, string? Lot, decimal Quantity)>();
        for (var i = 0; i < input.Count; i++)
        {
            var line = input[i];
            if (line.ProductId is not { } productId || !products.TryGetValue(productId, out var product))
            {
                errors.Add($"Línea {i + 1}: el artículo no existe.");
                continue;
            }

            var lot = product.TracksLots && !string.IsNullOrWhiteSpace(line.Lot) ? line.Lot.Trim() : null;
            if (product.TracksLots && lot is null)
            {
                errors.Add($"Línea {i + 1}: {product.Sku} se controla por lotes, indica el lote.");
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
            }

            // The same item and lot scanned twice becomes one line.
            var existing = lines.FindIndex(l => l.ProductId == productId && string.Equals(l.Lot, lot, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                lines[existing] = lines[existing] with { Quantity = lines[existing].Quantity + line.Quantity };
            }
            else
            {
                lines.Add((productId, lot, line.Quantity));
            }
        }

        if (errors.Count > 0)
        {
            throw new MovementRejectedException(
                StatusCodes.Status400BadRequest, "Revisa las líneas de la entrada.", new Dictionary<string, string[]> { ["lines"] = [.. errors] });
        }

        return lines;
    }

    private async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        var count = await db.Receipts.CountAsync(cancellationToken);
        for (var n = count + 1; ; n++)
        {
            var code = $"ENT-{n:D4}";
            if (!await db.Receipts.AnyAsync(r => r.Code == code, cancellationToken))
            {
                return code;
            }
        }
    }

    private static MovementRejectedException Invalid(string field, string message) =>
        new(StatusCodes.Status400BadRequest, message, new Dictionary<string, string[]> { [field] = [message] });
}
