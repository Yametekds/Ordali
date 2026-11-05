using System.Net;
using Ordali.Client.Models;

namespace Ordali.Client.Demo;

// Distributors and orders for the demo mode, following the same rules as the API's purchasing module.
public sealed partial class DemoOrdaliApi
{
    private readonly List<Supplier> _suppliers = [.. DemoData.Suppliers()];
    private readonly List<DemoOrder> _orders = [];

    public Task<IReadOnlyList<Supplier>> GetSuppliersAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var user = EnsureSignedIn();
        if (!user.Can(Permissions.OrdersView) && !user.Can(Permissions.OrdersEdit) && !user.Can(Permissions.ProductsEdit))
        {
            throw Forbidden();
        }

        lock (_lock)
        {
            return Result<IReadOnlyList<Supplier>>([.. _suppliers.Where(s => !activeOnly || s.Active).OrderBy(s => s.Name, StringComparer.CurrentCulture)]);
        }
    }

    public Task<Supplier> CreateSupplierAsync(SupplierInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var supplier = ToSupplier(Guid.NewGuid(), input);
            _suppliers.Add(supplier);
            return Result(supplier);
        }
    }

    public Task<Supplier> UpdateSupplierAsync(Guid id, SupplierInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var index = _suppliers.IndexOf(FindSupplier(id));
            var supplier = ToSupplier(id, input);
            _suppliers[index] = supplier;
            return Result(supplier);
        }
    }

    public Task<IReadOnlyList<ReorderSuggestion>> GetReorderSuggestionsAsync(Guid? supplierId = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var onOrder = _orders
                .Where(o => o.Status == PurchaseOrderStatus.Sent)
                .SelectMany(o => o.Lines)
                .GroupBy(l => l.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

            var suggestions = _products
                .Where(p => p.MinStock > 0 && (supplierId is null || p.SupplierId == supplierId))
                .Select(p => (Product: p, Available: _stock.Where(s => s.ProductId == p.Id).Sum(s => s.Quantity), OnOrder: onOrder.GetValueOrDefault(p.Id)))
                .Where(x => ReorderRules.IsShort(x.Available, x.OnOrder, x.Product.MinStock))
                .GroupBy(x => x.Product.SupplierId)
                .Select(g => new ReorderSuggestion(
                    g.Key,
                    g.Key is { } id ? _suppliers.FirstOrDefault(s => s.Id == id)?.Name : null,
                    [.. g.OrderBy(x => x.Product.Sku, StringComparer.Ordinal).Select(x => new ReorderLine(
                        x.Product.Id, x.Product.Sku, x.Product.Name, x.Product.Unit, x.Available, x.OnOrder, x.Product.MinStock, x.Product.MaxStock,
                        ReorderRules.Suggested(x.Available, x.OnOrder, x.Product.MinStock, x.Product.MaxStock)))]))
                .OrderBy(s => s.SupplierId is null)
                .ThenBy(s => s.SupplierName, StringComparer.CurrentCulture)
                .ToList();
            return Result<IReadOnlyList<ReorderSuggestion>>(suggestions);
        }
    }

    public Task<IReadOnlyList<PurchaseOrder>> GetPurchaseOrdersAsync(PurchaseOrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersView);
        lock (_lock)
        {
            return Result<IReadOnlyList<PurchaseOrder>>([.. _orders
                .Where(o => status is null || o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .Select(ToOrder)]);
        }
    }

    public Task<PurchaseOrder> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersView);
        lock (_lock)
        {
            return Result(ToOrder(FindOrder(id)));
        }
    }

    public Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrderInput input, CancellationToken cancellationToken = default)
    {
        var user = Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            var order = new DemoOrder { Id = Guid.NewGuid(), Code = NextOrderCode(), UserName = user.Name, CreatedAt = now };
            Fill(order, input);
            order.UpdatedAt = now;
            _orders.Add(order);
            return Result(ToOrder(order));
        }
    }

    public Task<PurchaseOrder> UpdatePurchaseOrderAsync(Guid id, PurchaseOrderInput input, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var order = FindOrder(id);
            if (order.Status != PurchaseOrderStatus.Draft)
            {
                throw new ApiException(HttpStatusCode.Conflict, $"El pedido {order.Code} ya no está en borrador y no se puede cambiar.");
            }

            Fill(order, input);
            order.UpdatedAt = _time.GetUtcNow();
            return Result(ToOrder(order));
        }
    }

    public Task<PurchaseOrder> ChangePurchaseOrderStatusAsync(Guid id, PurchaseOrderStatus status, CancellationToken cancellationToken = default)
    {
        Ensure(Permissions.OrdersEdit);
        lock (_lock)
        {
            var order = FindOrder(id);
            if (!ReorderRules.CanMove(order.Status, status))
            {
                throw new ApiException(
                    HttpStatusCode.Conflict,
                    $"El pedido {order.Code} está {ReorderRules.StatusName(order.Status)} y no puede pasar a {ReorderRules.StatusName(status)}.");
            }

            var now = _time.GetUtcNow();
            order.Status = status;
            order.UpdatedAt = now;
            if (status == PurchaseOrderStatus.Sent)
            {
                order.SentAt = now;
            }

            return Result(ToOrder(order));
        }
    }

    private void Fill(DemoOrder order, PurchaseOrderInput input)
    {
        var supplier = input.SupplierId is { } supplierId ? _suppliers.FirstOrDefault(s => s.Id == supplierId) : null;
        if (supplier is null)
        {
            throw Invalid(nameof(PurchaseOrderInput.SupplierId), "Elige el distribuidor.");
        }

        if (!supplier.Active && supplier.Id != order.SupplierId)
        {
            throw Invalid(nameof(PurchaseOrderInput.SupplierId), $"{supplier.Name} está dado de baja.");
        }

        if (input.Lines.Count == 0)
        {
            throw Invalid(nameof(PurchaseOrderInput.Lines), "Añade al menos un artículo.");
        }

        var errors = new List<string>();
        var lines = new List<DemoOrderLine>();
        for (var i = 0; i < input.Lines.Count; i++)
        {
            var line = input.Lines[i];
            if (line.ProductId is not { } productId || _products.All(p => p.Id != productId))
            {
                errors.Add($"Línea {i + 1}: el artículo no existe.");
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add($"Línea {i + 1}: la cantidad tiene que ser mayor que cero.");
                continue;
            }

            if (lines.FirstOrDefault(l => l.ProductId == productId) is { } existing)
            {
                existing.Quantity += line.Quantity;
                continue;
            }

            lines.Add(new DemoOrderLine { ProductId = productId, Quantity = line.Quantity, SuggestedQuantity = line.SuggestedQuantity });
        }

        if (errors.Count > 0)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Revisa las líneas del pedido.", new Dictionary<string, string[]> { ["lines"] = [.. errors] });
        }

        order.SupplierId = supplier.Id;
        order.DeliveryDate = input.DeliveryDate;
        order.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        order.Lines = lines;
    }

    private Supplier ToSupplier(Guid id, SupplierInput input)
    {
        var name = input.Name.Trim();
        if (_suppliers.Any(s => s.Id != id && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid(nameof(SupplierInput.Name), "Ya hay un distribuidor con ese nombre.");
        }

        return new Supplier(id, name, Clean(input.ContactName), Clean(input.Email), Clean(input.Phone), input.Active);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string NextOrderCode()
    {
        var last = _orders
            .Select(o => o.Code.StartsWith("PED-", StringComparison.Ordinal) && int.TryParse(o.Code.AsSpan(4), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return $"PED-{last + 1:D4}";
    }

    private Supplier FindSupplier(Guid id) =>
        _suppliers.FirstOrDefault(s => s.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "El distribuidor no existe.");

    private DemoOrder FindOrder(Guid id) =>
        _orders.FirstOrDefault(o => o.Id == id) ?? throw new ApiException(HttpStatusCode.NotFound, "El pedido no existe.");

    private PurchaseOrder ToOrder(DemoOrder order) => new(
        order.Id,
        order.Code,
        order.SupplierId,
        _suppliers.FirstOrDefault(s => s.Id == order.SupplierId)?.Name ?? "",
        order.Status,
        order.DeliveryDate,
        order.Notes,
        order.UserName,
        order.CreatedAt,
        order.UpdatedAt,
        order.SentAt,
        [.. order.Lines.Select(l =>
        {
            var product = FindProduct(l.ProductId);
            return new PurchaseOrderLine(product.Id, product.Sku, product.Name, product.Unit, l.Quantity, l.SuggestedQuantity);
        })]);

    // The same two orders as the API's seed: one received, one on its way.
    private void SeedOrders()
    {
        var now = _time.GetUtcNow();
        _orders.Add(SeedOrder(1, 1, PurchaseOrderStatus.Received, now.AddDays(-4), null, (1, 48, 48)));
        _orders.Add(SeedOrder(2, 2, PurchaseOrderStatus.Sent, now.AddDays(-1), DateOnly.FromDateTime(now.AddDays(2).Date), (6, 20, null)));
    }

    private static DemoOrder SeedOrder(
        int n, int supplier, PurchaseOrderStatus status, DateTimeOffset at, DateOnly? delivery, params (int Product, decimal Quantity, decimal? Suggested)[] lines) => new()
    {
        Id = DemoData.PurchaseOrderId(n),
        Code = $"PED-{n:D4}",
        SupplierId = DemoData.SupplierId(supplier),
        Status = status,
        DeliveryDate = delivery,
        UserName = "Marta Gil",
        CreatedAt = at,
        UpdatedAt = at,
        SentAt = at,
        Lines = [.. lines.Select(l => new DemoOrderLine { ProductId = DemoData.ProductId(l.Product), Quantity = l.Quantity, SuggestedQuantity = l.Suggested })],
    };

    private sealed class DemoOrder
    {
        public Guid Id { get; init; }

        public required string Code { get; init; }

        public Guid SupplierId { get; set; }

        public PurchaseOrderStatus Status { get; set; }

        public DateOnly? DeliveryDate { get; set; }

        public string? Notes { get; set; }

        public required string UserName { get; init; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? SentAt { get; set; }

        public List<DemoOrderLine> Lines { get; set; } = [];
    }

    private sealed class DemoOrderLine
    {
        public Guid ProductId { get; init; }

        public decimal Quantity { get; set; }

        public decimal? SuggestedQuantity { get; init; }
    }
}
