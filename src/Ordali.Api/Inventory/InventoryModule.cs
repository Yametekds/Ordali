using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Inventory;

public static class InventoryModule
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/stock", StockAsync)
            .RequireAuthorization(Permissions.StockView)
            .WithTags("Inventario")
            .WithSummary("Stock por artículo, ubicación y lote. Las líneas a cero no se devuelven.");

        var movements = api.MapGroup("/movements").WithTags("Inventario");
        movements.MapGet("/", MovementsAsync)
            .RequireAuthorization(Permissions.MovementsView)
            .WithSummary("Últimos movimientos, del más reciente al más antiguo, con quién los hizo. Filtra por artículo o por usuario.");
        movements.MapPost("/", RecordAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<MovementInput>()
            .WithSummary("Registra una salida, traspaso o ajuste y actualiza el stock. Las entradas van por /receipts.");
    }

    private static async Task<IResult> StockAsync(Guid? productId, Guid? locationId, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var items = await (
            from line in db.StockLines.AsNoTracking()
            join product in db.Products on line.ProductId equals product.Id
            join location in db.Locations on line.LocationId equals location.Id
            where line.Quantity != 0
                && (productId == null || line.ProductId == productId)
                && (locationId == null || line.LocationId == locationId)
            orderby product.Sku, location.Code
            select new StockItemDto(product.Id, product.Sku, product.Name, location.Id, location.Code, line.Lot, line.Quantity))
            .ToListAsync(cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> MovementsAsync(Guid? productId, Guid? userId, OrdaliDbContext db, CancellationToken cancellationToken, int take = 50)
    {
        take = Math.Clamp(take, 1, 500);
        var latest = db.StockMovements
            .Where(m => productId == null || m.ProductId == productId)
            .Where(m => userId == null || m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take);
        var items = await Project(db, latest).ToListAsync(cancellationToken);
        return Results.Ok(items);
    }

    private static async Task<IResult> RecordAsync(
        MovementInput input, ClaimsPrincipal user, AccessService access, MovementService service, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        if (input.Type == MovementType.Receipt)
        {
            return Validation.Invalid(nameof(MovementInput.Type), "Las entradas se registran en Entradas (POST /api/v1/receipts) y se ubican con traslados.");
        }

        if (input.Type == MovementType.Adjustment && (await access.GetAsync(user, cancellationToken))?.Has(Permissions.MovementsAdjust) != true)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, detail: "No tienes permiso para registrar ajustes.");
        }

        try
        {
            var movement = await service.RecordAsync(input, user.UserId(), user.Identity!.Name!, cancellationToken);
            var dto = await Project(db, db.StockMovements.Where(m => m.Id == movement.Id)).FirstAsync(cancellationToken);
            return Results.Created($"/api/v1/movements/{dto.Id}", dto);
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

    private static IQueryable<MovementDto> Project(OrdaliDbContext db, IQueryable<StockMovement> movements) =>
        from m in movements.AsNoTracking()
        join product in db.Products on m.ProductId equals product.Id
        join source in db.Locations on m.FromLocationId equals (Guid?)source.Id into sources
        from source in sources.DefaultIfEmpty()
        join target in db.Locations on m.ToLocationId equals (Guid?)target.Id into targets
        from target in targets.DefaultIfEmpty()
        orderby m.CreatedAt descending
        select new MovementDto(
            m.Id, m.Type, m.ProductId, product.Sku, m.Lot,
            source == null ? null : source.Code,
            target == null ? null : target.Code,
            m.Quantity, m.Reason, m.UserName, m.CreatedAt, m.Reference);
}
