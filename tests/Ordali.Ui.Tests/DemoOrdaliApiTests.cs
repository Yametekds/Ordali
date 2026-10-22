using System.Net;
using Microsoft.Extensions.Time.Testing;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Demo;
using Ordali.Client.Models;

namespace Ordali.Ui.Tests;

public class DemoOrdaliApiTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly SessionState _session;
    private readonly DemoOrdaliApi _api;

    public DemoOrdaliApiTests()
    {
        _session = new SessionState(_time);
        _api = new DemoOrdaliApi(_session, _time);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.LoginAsync(new LoginRequest("admin@demo.local", "nope")));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
    }

    [Fact]
    public async Task Data_needs_a_signed_in_user()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.GetProductsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
    }

    [Fact]
    public async Task Session_expires()
    {
        await SignInAsync("mozo@demo.local");
        _time.Advance(TimeSpan.FromHours(9));

        Assert.False(_session.IsAuthenticated);
        await Assert.ThrowsAsync<ApiException>(() => _api.GetProductsAsync());
    }

    [Fact]
    public async Task Transfer_moves_stock_and_records_who_did_it()
    {
        await SignInAsync("mozo@demo.local");
        var (product, from, to) = await FindAsync("TOR-M8", "A-02-01", "PCK-02");
        var before = await QuantityAsync(product, from) + await QuantityAsync(product, to);

        var movement = await _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Transfer,
            ProductId = product,
            FromLocationId = from,
            ToLocationId = to,
            Quantity = 5,
        });

        Assert.Equal(40, await QuantityAsync(product, from));
        Assert.Equal(before, await QuantityAsync(product, from) + await QuantityAsync(product, to));
        Assert.Equal("Javier Ruiz", movement.UserName);
        Assert.Equal(_time.GetUtcNow(), movement.CreatedAt);
    }

    [Fact]
    public async Task Shipment_larger_than_stock_is_rejected_and_stock_is_unchanged()
    {
        await SignInAsync("mozo@demo.local");
        var (product, from, _) = await FindAsync("TOR-M8", "A-02-01", "A-02-01");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Shipment,
            ProductId = product,
            FromLocationId = from,
            Quantity = 46,
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
        Assert.Equal(45, await QuantityAsync(product, from));
    }

    [Fact]
    public async Task Lot_tracked_items_need_a_lot()
    {
        await SignInAsync("mozo@demo.local");
        var (product, from, to) = await FindAsync("ACE-001", "A-01-01", "PCK-01");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Transfer,
            ProductId = product,
            FromLocationId = from,
            ToLocationId = to,
            Quantity = 10,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Contains(nameof(MovementInput.Lot), ex.Errors.Keys);
    }

    [Fact]
    public async Task Receipts_wait_on_the_dock_until_they_are_put_away()
    {
        await SignInAsync("mozo@demo.local");
        var (product, dock, aisle) = await FindAsync("CIN-050", "REC-01", "B-01-01");

        var receipt = await _api.CreateReceiptAsync(new ReceiptInput { Code = "entrada3", Lines = [new() { ProductId = product, Quantity = 12 }] });
        var onDock = await QuantityAsync(product, dock);
        var partial = await _api.PutAwayAsync(receipt.Id, new PutAwayInput { ProductId = product, ToLocationId = aisle, Quantity = 5 });
        var done = await _api.PutAwayAsync(receipt.Id, new PutAwayInput { ProductId = product, ToLocationId = aisle, Quantity = 7 });

        Assert.Equal("ENTRADA3", receipt.Code);
        Assert.Equal(12, onDock);
        Assert.Equal(7, partial.Pending);
        Assert.Equal(ReceiptStatus.Completed, done.Status);
        Assert.Equal(0, await QuantityAsync(product, dock));
        Assert.Equal(40, await QuantityAsync(product, aisle));
        Assert.DoesNotContain(await _api.GetReceiptsAsync(pendingOnly: true), r => r.Id == receipt.Id);
        Assert.All((await _api.GetMovementsAsync(take: 3)).Select(m => m.Reference), r => Assert.Equal("ENTRADA3", r));
    }

    [Fact]
    public async Task Put_away_cannot_move_more_than_is_left()
    {
        await SignInAsync("mozo@demo.local");
        var receipt = (await _api.GetReceiptsAsync(pendingOnly: true)).Single(r => r.Code == "ENTRADA2");
        var screws = receipt.Lines.Single(l => l.Sku == "TOR-M8");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.PutAwayAsync(receipt.Id, new PutAwayInput
        {
            ProductId = screws.ProductId, ToLocationId = screws.DefaultLocationId, Quantity = 21,
        }));

        Assert.Equal("A-02-01", screws.DefaultLocationCode);
        Assert.Contains(nameof(PutAwayInput.Quantity), ex.Errors.Keys);
    }

    [Fact]
    public async Task Receipts_are_not_plain_movements()
    {
        await SignInAsync("mozo@demo.local");
        var (product, _, dock) = await FindAsync("CIN-050", "REC-01", "REC-01");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Receipt, ProductId = product, ToLocationId = dock, Quantity = 1,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Duplicate_location_code_needs_confirmation()
    {
        await SignInAsync("encargado@demo.local");

        var warning = await Assert.ThrowsAsync<ApiException>(() => _api.CreateLocationAsync(new LocationInput { Code = "a-01-01", Zone = "Pasillo A" }));
        await _api.CreateLocationAsync(new LocationInput { Code = "a-01-01", Zone = "Pasillo A", AllowDuplicate = true });

        Assert.Equal(HttpStatusCode.Conflict, warning.Status);
        Assert.Equal(2, (await _api.GetLocationsAsync()).Count(l => l.Code == "A-01-01"));
    }

    [Fact]
    public async Task Adjustment_cannot_leave_negative_stock()
    {
        await SignInAsync("admin@demo.local");
        var (product, _, location) = await FindAsync("CIN-050", "B-01-01", "B-01-01");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Adjustment,
            ProductId = product,
            ToLocationId = location,
            Quantity = -100,
            Reason = "Recuento",
        }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
    }

    [Fact]
    public async Task Pickers_cannot_change_the_catalog()
    {
        await SignInAsync("mozo@demo.local");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateProductAsync(new ProductInput { Sku = "NEW-1", Name = "Nuevo" }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task Sku_must_be_unique()
    {
        await SignInAsync("admin@demo.local");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateProductAsync(new ProductInput { Sku = "ace-001", Name = "Duplicado" }));

        Assert.Contains(nameof(ProductInput.Sku), ex.Errors.Keys);
    }

    [Fact]
    public async Task Search_matches_sku_name_and_gtin()
    {
        await SignInAsync("admin@demo.local");

        Assert.Single(await _api.GetProductsAsync("aceite"));
        Assert.Single(await _api.GetProductsAsync("tor-m8"));
        Assert.Single(await _api.GetProductsAsync("8430000000020"));
    }

    [Fact]
    public async Task Pickers_cannot_make_adjustments()
    {
        await SignInAsync("mozo@demo.local");
        var (product, _, location) = await FindAsync("CIN-050", "B-01-01", "B-01-01");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateMovementAsync(new MovementInput
        {
            Type = MovementType.Adjustment, ProductId = product, ToLocationId = location, Quantity = -1, Reason = "Rotura",
        }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task New_profile_takes_category_and_permissions_from_the_template()
    {
        await SignInAsync("encargado@demo.local");
        var template = (await _api.GetTemplatesAsync()).Single(t => t.Name == "Mozo de almacén");

        var user = await _api.CreateUserAsync(new NewUserInput { Name = "Rosa Vidal", Email = "rosa@demo.local", Password = "Segura123", TemplateId = template.Id });

        Assert.Equal(UserCategory.Operator, user.Category);
        Assert.Equal(template.Permissions, user.Permissions);
        Assert.Equal("Mozo de almacén", user.TemplateName);
        await SignInAsync("rosa@demo.local", "Segura123");
        Assert.Equal("Rosa Vidal", _session.Current!.User.Name);
    }

    [Fact]
    public async Task Only_admins_manage_templates_and_profiles()
    {
        await SignInAsync("encargado@demo.local");
        var mozo = (await _api.GetUsersAsync()).Single(u => u.Email == "mozo@demo.local");

        var create = await Assert.ThrowsAsync<ApiException>(() => _api.CreateTemplateAsync(new ProfileTemplateInput { Name = "X", Permissions = [Permissions.StockView] }));
        var promote = await Assert.ThrowsAsync<ApiException>(() => _api.UpdateUserAccessAsync(mozo.Id, new UserAccessInput { Category = UserCategory.Supervisor }));

        Assert.Equal(HttpStatusCode.Forbidden, create.Status);
        Assert.Equal(HttpStatusCode.Forbidden, promote.Status);
    }

    [Fact]
    public async Task Admin_promotes_and_grants_special_permissions_that_apply_at_once()
    {
        await SignInAsync("admin@demo.local");
        var mozo = (await _api.GetUsersAsync()).Single(u => u.Email == "mozo@demo.local");
        var access = UserAccessInput.From(mozo);
        access.Category = UserCategory.Supervisor;
        access.Permissions.Add(Permissions.MovementsAdjust);

        var updated = await _api.UpdateUserAccessAsync(mozo.Id, access);

        Assert.Equal(UserCategory.Supervisor, updated.Category);
        Assert.Contains(Permissions.MovementsAdjust, updated.Permissions);
        await SignInAsync("mozo@demo.local");
        Assert.True((await _api.GetMeAsync()).Can(Permissions.MovementsAdjust));
    }

    [Fact]
    public async Task Deactivated_profiles_cannot_sign_in()
    {
        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.LoginAsync(new LoginRequest("pedro.sanz@demo.local", "Demo1234!")));

        Assert.Equal(HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task Admin_cannot_demote_themself()
    {
        await SignInAsync("admin@demo.local");
        var me = _session.Current!.User;

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.UpdateUserAccessAsync(me.Id, new UserAccessInput { Category = UserCategory.Supervisor }));

        Assert.Equal(HttpStatusCode.Conflict, ex.Status);
    }

    [Fact]
    public async Task Deleting_a_template_keeps_the_profiles_permissions()
    {
        await SignInAsync("admin@demo.local");
        var template = (await _api.GetTemplatesAsync()).Single(t => t.Name == "Solo consulta");

        await _api.DeleteTemplateAsync(template.Id);

        var ana = (await _api.GetUsersAsync()).Single(u => u.Email == "ana.lopez@demo.local");
        Assert.Null(ana.TemplateId);
        Assert.Equal(template.Permissions, ana.Permissions);
    }

    [Fact]
    public async Task Templates_cannot_create_admins()
    {
        await SignInAsync("admin@demo.local");

        var ex = await Assert.ThrowsAsync<ApiException>(() => _api.CreateTemplateAsync(new ProfileTemplateInput { Name = "Jefe", Category = UserCategory.Admin, Permissions = [Permissions.StockView] }));

        Assert.Contains(nameof(ProfileTemplateInput.Category), ex.Errors.Keys);
    }

    private async Task SignInAsync(string email, string password = "Demo1234!") =>
        _session.Set(await _api.LoginAsync(new LoginRequest(email, password)));


    private async Task<(Guid Product, Guid From, Guid To)> FindAsync(string sku, string from, string to)
    {
        var products = await _api.GetProductsAsync(sku);
        var locations = await _api.GetLocationsAsync();
        return (products.Single(p => p.Sku == sku).Id, locations.Single(l => l.Code == from).Id, locations.Single(l => l.Code == to).Id);
    }

    private async Task<decimal> QuantityAsync(Guid product, Guid location) =>
        (await _api.GetStockAsync(product, location)).Sum(s => s.Quantity);
}
