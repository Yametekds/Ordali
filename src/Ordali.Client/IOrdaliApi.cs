using Ordali.Client.Models;

namespace Ordali.Client;

/// <summary>
/// What the user interfaces need from the warehouse API. There is an HTTP implementation
/// for the real API and an in-memory one so the web demo works without a server.
/// </summary>
public interface IOrdaliApi
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>The signed-in user with their current permissions, which an admin may have changed since login.</summary>
    Task<UserInfo> GetMeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<Product> GetProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Product> CreateProductAsync(ProductInput input, CancellationToken cancellationToken = default);

    Task<Product> UpdateProductAsync(Guid id, ProductInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Site>> GetSitesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Location>> GetLocationsAsync(Guid? siteId = null, CancellationToken cancellationToken = default);

    /// <summary>Answers 409 when the code already exists, unless <see cref="LocationInput.AllowDuplicate"/> is set.</summary>
    Task<Location> CreateLocationAsync(LocationInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockItem>> GetStockAsync(Guid? productId = null, Guid? locationId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Movement>> GetMovementsAsync(int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Shipments, transfers and adjustments. Receipts go through <see cref="CreateReceiptAsync"/>.</summary>
    Task<Movement> CreateMovementAsync(MovementInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Receipt>> GetReceiptsAsync(bool pendingOnly = false, CancellationToken cancellationToken = default);

    Task<Receipt> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Brings the goods into the receiving location; they wait there until they are put away.</summary>
    Task<Receipt> CreateReceiptAsync(ReceiptInput input, CancellationToken cancellationToken = default);

    /// <summary>Transfers units of one receipt line from the dock to their location.</summary>
    Task<Receipt> PutAwayAsync(Guid receiptId, PutAwayInput input, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<ProfileTemplate> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProfileTemplate> CreateTemplateAsync(ProfileTemplateInput input, CancellationToken cancellationToken = default);

    Task<ProfileTemplate> UpdateTemplateAsync(Guid id, ProfileTemplateInput input, CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<UserProfile> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserProfile> CreateUserAsync(NewUserInput input, CancellationToken cancellationToken = default);

    Task<UserProfile> UpdateUserAccessAsync(Guid id, UserAccessInput input, CancellationToken cancellationToken = default);
}
