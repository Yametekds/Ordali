using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Inventory;

public static class ReceiptsModule
{
    public static void MapReceiptEndpoints(this IEndpointRouteBuilder api)
    {
        var receipts = api.MapGroup("/receipts").WithTags("Entradas");

        receipts.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.MovementsView)
            .WithSummary("Entradas, de la más reciente a la más antigua. 'pending=true' devuelve solo las que quedan por ubicar; 'code' busca por código exacto.");
        receipts.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Permissions.MovementsView)
            .WithName("GetReceipt");
        receipts.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<ReceiptInput>()
            .WithSummary("Registra la llegada de mercancía. El stock queda en la ubicación de recepción hasta que se traslada.");
        receipts.MapPost("/{id:guid}/putaway", PutAwayAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<PutAwayInput>()
            .WithSummary("Traslada unidades de una línea de la entrada desde recepción a su ubicación.");
    }

    private static async Task<IResult> ListAsync(bool? pending, string? code, OrdaliDbContext db, CancellationToken cancellationToken, int take = 50)
    {
        take = Math.Clamp(take, 1, 200);
        var query = db.Receipts.AsNoTracking().Include(r => r.Lines).AsQueryable();
        if (pending == true)
        {
            query = query.Where(r => r.CompletedAt == null);
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = ReceiptService.Normalize(code);
            query = query.Where(r => r.Code == normalized);
        }

        var receipts = await query.OrderByDescending(r => r.CreatedAt).Take(take).ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(db, receipts, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var receipt = await db.Receipts.AsNoTracking().Include(r => r.Lines).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return receipt is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "La entrada no existe.")
            : Results.Ok((await ToDtosAsync(db, [receipt], cancellationToken))[0]);
    }

    private static Task<IResult> CreateAsync(
        ReceiptInput input, ClaimsPrincipal user, ReceiptService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () =>
        {
            var receipt = await service.CreateAsync(input, user.UserId(), user.Identity!.Name!, cancellationToken);
            var dto = (await ToDtosAsync(db, [receipt], cancellationToken))[0];
            return Results.CreatedAtRoute("GetReceipt", new { id = receipt.Id }, dto);
        });

    private static Task<IResult> PutAwayAsync(
        Guid id, PutAwayInput input, ClaimsPrincipal user, ReceiptService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () =>
        {
            var receipt = await service.PutAwayAsync(id, input, user.UserId(), user.Identity!.Name!, cancellationToken);
            return Results.Ok((await ToDtosAsync(db, [receipt], cancellationToken))[0]);
        });

    private static async Task<IResult> HandleAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MovementRejectedException ex) when (ex.Errors is not null)
        {
            return Results.ValidationProblem(ex.Errors, detail: ex.Message, title: "Revisa los datos enviados.");
        }
        catch (MovementRejectedException ex)
        {
            return Results.Problem(statusCode: ex.Status, detail: ex.Message);
        }
    }

    private static async Task<List<ReceiptDto>> ToDtosAsync(OrdaliDbContext db, IReadOnlyList<Receipt> receipts, CancellationToken cancellationToken)
    {
        var productIds = receipts.SelectMany(r => r.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var locationIds = receipts.Select(r => r.DockLocationId)
            .Concat(products.Values.Where(p => p.DefaultLocationId != null).Select(p => p.DefaultLocationId!.Value))
            .Distinct()
            .ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);

        return [.. receipts.Select(r => new ReceiptDto(
            r.Id,
            r.Code,
            r.Reference,
            r.DockLocationId,
            locations[r.DockLocationId],
            r.CompletedAt is null ? ReceiptStatus.Pending : ReceiptStatus.Completed,
            r.UserName,
            r.CreatedAt,
            r.CompletedAt,
            [.. r.Lines
                .Select(l => (Line: l, Product: products[l.ProductId]))
                .OrderBy(x => x.Product.Sku, StringComparer.Ordinal)
                .ThenBy(x => x.Line.Lot, StringComparer.Ordinal)
                .Select(x => new ReceiptLineDto(
                    x.Product.Id,
                    x.Product.Sku,
                    x.Product.Name,
                    x.Product.Gtin,
                    x.Product.Unit,
                    x.Line.Lot,
                    x.Line.Quantity,
                    x.Line.PutAwayQuantity,
                    x.Line.Quantity - x.Line.PutAwayQuantity,
                    x.Product.DefaultLocationId,
                    x.Product.DefaultLocationId is { } d ? locations.GetValueOrDefault(d) : null))]))];
    }
}
