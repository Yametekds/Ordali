using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;

namespace Ordali.Api.Catalog;

public static class CatalogModule
{
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        var products = api.MapGroup("/products").WithTags("Artículos");

        products.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.ProductsView)
            .WithSummary("Lista los artículos. 'search' busca en SKU, nombre y código de barras (GTIN).");
        products.MapGet("/{id:guid}", GetAsync).RequireAuthorization(Permissions.ProductsView).WithName("GetProduct");
        products.MapPost("/", CreateAsync).RequireAuthorization(Permissions.ProductsEdit).Validate<ProductInput>();
        products.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Permissions.ProductsEdit).Validate<ProductInput>();
    }

    private static async Task<IResult> ListAsync(string? search, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            // ToUpper() is translated to SQL UPPER(), so the culture overloads the analyzer asks for don't apply.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(p => p.Sku.ToUpper().Contains(term) || p.Name.ToUpper().Contains(term) || (p.Gtin != null && p.Gtin.Contains(term)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var list = await query.OrderBy(p => p.Sku).ToListAsync(cancellationToken);
        return Results.Ok(list.Select(ProductDto.From));
    }

    private static async Task<IResult> GetAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return product is null ? NotFound() : Results.Ok(ProductDto.From(product));
    }

    private static async Task<IResult> CreateAsync(ProductInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var product = new Product { Id = Guid.NewGuid(), Sku = "", Name = "", Unit = "" };
        Apply(product, input);
        if (await SkuTakenAsync(db, product.Sku, null, cancellationToken))
        {
            return SkuTaken();
        }

        if (await UnknownLocationAsync(db, product.DefaultLocationId, cancellationToken))
        {
            return UnknownLocation();
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return Results.CreatedAtRoute("GetProduct", new { id = product.Id }, ProductDto.From(product));
    }

    private static async Task<IResult> UpdateAsync(Guid id, ProductInput input, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        Apply(product, input);
        if (await SkuTakenAsync(db, product.Sku, id, cancellationToken))
        {
            return SkuTaken();
        }

        if (await UnknownLocationAsync(db, product.DefaultLocationId, cancellationToken))
        {
            return UnknownLocation();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ProductDto.From(product));
    }

    private static void Apply(Product product, ProductInput input)
    {
        product.Sku = input.Sku.Trim().ToUpperInvariant();
        product.Name = input.Name.Trim();
        product.Gtin = string.IsNullOrWhiteSpace(input.Gtin) ? null : input.Gtin.Trim();
        product.Unit = input.Unit.Trim();
        product.TracksLots = input.TracksLots;
        product.MinStock = input.MinStock;
        product.DefaultLocationId = input.DefaultLocationId;
    }

    private static async Task<bool> UnknownLocationAsync(OrdaliDbContext db, Guid? locationId, CancellationToken cancellationToken) =>
        locationId is { } id && !await db.Locations.AnyAsync(l => l.Id == id, cancellationToken);

    private static IResult UnknownLocation() => Validation.Invalid(nameof(ProductInput.DefaultLocationId), "La ubicación no existe.");

    private static Task<bool> SkuTakenAsync(OrdaliDbContext db, string sku, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.Sku == sku && p.Id != exceptId, cancellationToken);

    private static IResult SkuTaken() => Validation.Invalid(nameof(ProductInput.Sku), "Ya existe un artículo con ese SKU.");

    private static IResult NotFound() => Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "El artículo no existe.");
}
