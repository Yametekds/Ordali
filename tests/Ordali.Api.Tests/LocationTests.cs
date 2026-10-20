using System.Net;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class LocationTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Supervisor_creates_a_location()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var location = await api.CreateLocationAsync(new LocationInput { Code = " c-01-01 ", Zone = "Pasillo C" });

        Assert.Equal("C-01-01", location.Code);
        Assert.Equal(LocationType.Storage, location.Type);
        Assert.Contains(await api.GetLocationsAsync(), l => l.Id == location.Id);
    }

    [Fact]
    public async Task Existing_code_warns_first_and_can_still_be_created()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var warning = await Assert.ThrowsAsync<ApiException>(() => api.CreateLocationAsync(new LocationInput { Code = "pck-01", Zone = "Picking" }));
        var second = await api.CreateLocationAsync(new LocationInput { Code = "pck-01", Zone = "Picking", Type = LocationType.Picking, AllowDuplicate = true });

        Assert.Equal(HttpStatusCode.Conflict, warning.Status);
        Assert.Contains("PCK-01", warning.Message, StringComparison.Ordinal);
        Assert.Equal(2, (await api.GetLocationsAsync()).Count(l => l.Code == "PCK-01"));
        Assert.Equal("PCK-01", second.Code);
    }

    [Fact]
    public async Task Operators_cannot_create_locations()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateLocationAsync(new LocationInput { Code = "Z-99", Zone = "Z" }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task Products_keep_a_default_location()
    {
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);
        var input = ProductInput.From(await api.GetProductAsync(DemoIds.Product(DemoIds.Pallet)));
        input.DefaultLocationId = DemoIds.Location(DemoIds.AisleB1);

        var updated = await api.UpdateProductAsync(DemoIds.Product(DemoIds.Pallet), input);
        input.DefaultLocationId = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ApiException>(() => api.UpdateProductAsync(DemoIds.Product(DemoIds.Pallet), input));

        Assert.Equal(DemoIds.Location(DemoIds.AisleB1), updated.DefaultLocationId);
        Assert.Contains("defaultLocationId", ex.Errors.Keys);
    }
}
