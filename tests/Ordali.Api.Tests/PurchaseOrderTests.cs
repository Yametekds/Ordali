using System.Net;
using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Api.Tests;

public class PurchaseOrderTests
{
    private static readonly Guid Groceries = Guid.Parse("4b8e7a10-0000-4000-8000-300000000001");
    private static readonly Guid Hardware = Guid.Parse("4b8e7a10-0000-4000-8000-300000000002");

    [Fact]
    public async Task Items_below_their_safe_range_are_proposed_per_distributor()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var suggestions = await api.GetReorderSuggestionsAsync();

        // Rice: 60 in stock, range 80–200. Tape: 30 minus 2 damaged, range 50–150.
        Assert.Equal([Groceries, Hardware], suggestions.Select(s => s.SupplierId!.Value));
        var rice = Assert.Single(suggestions[0].Lines);
        Assert.Equal(("ARR-002", 60m, 140m), (rice.Sku, rice.Available, rice.SuggestedQuantity));
        var tape = Assert.Single(suggestions[1].Lines);
        Assert.Equal(("CIN-050", 28m, 122m), (tape.Sku, tape.Available, tape.SuggestedQuantity));
    }

    [Fact]
    public async Task Sent_orders_count_as_stock_so_the_shortage_is_not_ordered_twice()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var order = await api.CreatePurchaseOrderAsync(Order(Groceries, (DemoIds.Rice, 50, 140)));
        Assert.Contains(await api.GetReorderSuggestionsAsync(Groceries), s => s.Lines.Any(l => l.Sku == "ARR-002"));
        await api.ChangePurchaseOrderStatusAsync(order.Id, PurchaseOrderStatus.Sent);

        // 60 in stock + 50 on the way is above the minimum of 80.
        Assert.DoesNotContain(await api.GetReorderSuggestionsAsync(Groceries), s => s.Lines.Any(l => l.Sku == "ARR-002"));
    }

    [Fact]
    public async Task Draft_takes_proposed_and_hand_picked_items_and_locks_once_sent()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var draft = await api.CreatePurchaseOrderAsync(Order(Hardware, (DemoIds.Tape, 100, 122), (DemoIds.Gloves, 5, null), (DemoIds.Gloves, 5, null)));
        Assert.Equal(PurchaseOrderStatus.Draft, draft.Status);
        Assert.StartsWith("PED-", draft.Code, StringComparison.Ordinal);
        Assert.Equal([("CIN-050", 100m, (decimal?)122m), ("GUA-NIT", 10m, null)], draft.Lines.Select(l => (l.Sku, l.Quantity, l.SuggestedQuantity)));

        var changed = await api.UpdatePurchaseOrderAsync(draft.Id, Order(Hardware, (DemoIds.Tape, 130, 122)));
        Assert.Equal(130, Assert.Single(changed.Lines).Quantity);

        var sent = await api.ChangePurchaseOrderStatusAsync(draft.Id, PurchaseOrderStatus.Sent);
        Assert.NotNull(sent.SentAt);
        var locked = await Assert.ThrowsAsync<ApiException>(() => api.UpdatePurchaseOrderAsync(draft.Id, Order(Hardware, (DemoIds.Tape, 1, null))));
        Assert.Equal(HttpStatusCode.Conflict, locked.Status);

        await api.ChangePurchaseOrderStatusAsync(draft.Id, PurchaseOrderStatus.Received);
        var closed = await Assert.ThrowsAsync<ApiException>(() => api.ChangePurchaseOrderStatusAsync(draft.Id, PurchaseOrderStatus.Cancelled));
        Assert.Equal(HttpStatusCode.Conflict, closed.Status);
    }

    [Fact]
    public async Task Orders_need_a_distributor_and_valid_lines()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var empty = await Assert.ThrowsAsync<ApiException>(() => api.CreatePurchaseOrderAsync(new PurchaseOrderInput { SupplierId = Hardware }));
        var noSupplier = await Assert.ThrowsAsync<ApiException>(() => api.CreatePurchaseOrderAsync(Order(Guid.NewGuid(), (DemoIds.Tape, 1, null))));
        var zero = await Assert.ThrowsAsync<ApiException>(() => api.CreatePurchaseOrderAsync(Order(Hardware, (DemoIds.Tape, 0, null))));

        Assert.Contains("lines", empty.Errors.Keys);
        Assert.Contains("supplierId", noSupplier.Errors.Keys);
        Assert.Contains("Línea 1", zero.Errors["lines"][0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inactive_distributors_get_no_new_orders()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);
        var supplier = await api.CreateSupplierAsync(new SupplierInput { Name = "Envases Levante", Email = "pedidos@envases.example" });
        await api.UpdateSupplierAsync(supplier.Id, new SupplierInput { Name = "Envases Levante", Active = false });

        var error = await Assert.ThrowsAsync<ApiException>(() => api.CreatePurchaseOrderAsync(Order(supplier.Id, (DemoIds.Tape, 10, null))));

        Assert.Contains("dado de baja", error.Errors["supplierId"][0], StringComparison.Ordinal);
        Assert.DoesNotContain(await api.GetSuppliersAsync(activeOnly: true), s => s.Id == supplier.Id);
        var duplicate = await Assert.ThrowsAsync<ApiException>(() => api.CreateSupplierAsync(new SupplierInput { Name = "Envases Levante" }));
        Assert.Contains("name", duplicate.Errors.Keys);
    }

    [Fact]
    public async Task Products_keep_their_safe_range_and_distributor()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.AdminEmail);
        var pallet = await api.GetProductAsync(DemoIds.Product(DemoIds.Pallet));
        var input = ProductInput.From(pallet);
        input.MaxStock = 5;

        var invalid = await Assert.ThrowsAsync<ApiException>(() => api.UpdateProductAsync(pallet.Id, input));
        input.MaxStock = 40;
        input.SupplierId = Hardware;
        var saved = await api.UpdateProductAsync(pallet.Id, input);

        Assert.Contains("maxStock", invalid.Errors.Keys);
        Assert.Equal((40m, (Guid?)Hardware), (saved.MaxStock, saved.SupplierId));
    }

    [Fact]
    public async Task Pickers_cannot_see_orders()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.OperatorEmail);

        var error = await Assert.ThrowsAsync<ApiException>(() => api.GetPurchaseOrdersAsync());

        Assert.Equal(HttpStatusCode.Forbidden, error.Status);
    }

    [Fact]
    public async Task Demo_data_has_one_order_on_its_way()
    {
        using var factory = new OrdaliApiFactory();
        var api = await factory.SignInAsync(OrdaliApiFactory.SupervisorEmail);

        var open = await api.GetPurchaseOrdersAsync(PurchaseOrderStatus.Sent);

        var order = Assert.Single(open);
        Assert.Equal(("PED-0002", "Suministros Norte"), (order.Code, order.SupplierName));
        Assert.NotNull(order.DeliveryDate);
    }

    private static PurchaseOrderInput Order(Guid supplierId, params (int Product, decimal Quantity, decimal? Suggested)[] lines) => new()
    {
        SupplierId = supplierId,
        Lines = [.. lines.Select(l => new PurchaseOrderLineInput { ProductId = DemoIds.Product(l.Product), Quantity = l.Quantity, SuggestedQuantity = l.Suggested })],
    };
}
