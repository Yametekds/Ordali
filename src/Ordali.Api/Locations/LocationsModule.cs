using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Locations;

public static class LocationsModule
{
    public static void MapLocationsEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/sites", async (OrdaliDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.Sites.AsNoTracking()
                    .OrderBy(s => s.Code)
                    .Select(s => new SiteDto(s.Id, s.Code, s.Name))
                    .ToListAsync(cancellationToken)))
            .RequireAuthorization(Permissions.LocationsView)
            .WithTags("Ubicaciones")
            .WithSummary("Lista los almacenes.");

        api.MapGet("/locations", async (Guid? siteId, OrdaliDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.Locations.AsNoTracking()
                    .Where(l => siteId == null || l.SiteId == siteId)
                    .OrderBy(l => l.Code)
                    .Select(l => new LocationDto(l.Id, l.SiteId, l.Code, l.Zone, l.Type))
                    .ToListAsync(cancellationToken)))
            .RequireAuthorization(Permissions.LocationsView)
            .WithTags("Ubicaciones")
            .WithSummary("Lista las ubicaciones (huecos), opcionalmente de un almacén.");

        api.MapPost("/locations", CreateAsync)
            .RequireAuthorization(Permissions.LocationsEdit)
            .Validate<LocationInput>()
            .WithTags("Ubicaciones")
            .WithSummary("Crea una ubicación. Si el código ya existe responde 409 con un aviso; con allowDuplicate=true la crea igualmente.");
    }

    private static async Task<IResult> CreateAsync(LocationInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var site = input.SiteId is { } siteId
            ? await db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken)
            : await db.Sites.OrderBy(s => s.Code).FirstOrDefaultAsync(cancellationToken);
        if (site is null)
        {
            return Validation.Invalid(nameof(LocationInput.SiteId), "El almacén no existe.");
        }

        var code = input.Code.Trim().ToUpperInvariant();
        var existing = await db.Locations.AsNoTracking().FirstOrDefaultAsync(l => l.SiteId == site.Id && l.Code == code, cancellationToken);
        if (existing is not null && !input.AllowDuplicate)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "La ubicación ya existe.",
                detail: $"Ya existe la ubicación {existing.Code} en {existing.Zone}. Si creas otra con el mismo código, al escanearlo habrá que elegir entre las dos.");
        }

        var location = new Location { Id = Guid.NewGuid(), SiteId = site.Id, Code = code, Zone = input.Zone.Trim(), Type = input.Type };
        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/v1/locations/{location.Id}", new LocationDto(location.Id, location.SiteId, location.Code, location.Zone, location.Type));
    }
}
