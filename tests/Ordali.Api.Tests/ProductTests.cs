using System.Net;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class ProductTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Search_finds_a_product_by_barcode()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var products = await api.GetProductsAsync("8410000000017");

        var product = Assert.Single(products);
        Assert.Equal("ACE-001", product.Sku);
    }

    [Fact]
    public async Task Search_ignores_case()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var products = await api.GetProductsAsync("arroz");

        Assert.Equal("ARR-002", Assert.Single(products).Sku);
    }

    [Fact]
    public async Task Operator_cannot_change_the_catalogue()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateProductAsync(new ProductInput { Sku = "NEW-1", Name = "Nuevo" }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task Admin_creates_and_updates_a_product()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);

        var created = await api.CreateProductAsync(new ProductInput { Sku = " caf-250 ", Name = "Café molido 250 g", Gtin = "8410000000031", MinStock = 40 });
        var updated = await api.UpdateProductAsync(created.Id, new ProductInput { Sku = "CAF-250", Name = "Café molido natural 250 g", Gtin = "8410000000031", MinStock = 60 });

        Assert.Equal("CAF-250", created.Sku);
        Assert.Equal("Café molido natural 250 g", updated.Name);
        Assert.Equal(60, (await api.GetProductAsync(created.Id)).MinStock);
    }

    [Fact]
    public async Task Duplicate_sku_is_a_validation_error_on_the_field()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateProductAsync(new ProductInput { Sku = "tor-m8", Name = "Otro" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Equal(["Ya existe un artículo con ese SKU."], ex.Errors["sku"]);
    }

    [Fact]
    public async Task Invalid_input_returns_every_field_error()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateProductAsync(new ProductInput { Sku = "", Name = "", Gtin = "123" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains("sku", ex.Errors.Keys);
        Assert.Contains("name", ex.Errors.Keys);
        Assert.Contains("gtin", ex.Errors.Keys);
    }

    [Fact]
    public async Task Unknown_product_is_not_found()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.GetProductAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, ex.Status);
        Assert.Equal("El artículo no existe.", ex.Message);
    }
}
