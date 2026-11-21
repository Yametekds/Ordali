using System.ComponentModel.DataAnnotations;

namespace Ordali.Api.Shipping;

public sealed record DeliveryNoteLineDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    string? Gtin,
    string? Lot,
    decimal Quantity,
    int Pallet);

public sealed record DeliveryNoteDto(
    Guid Id,
    string Code,
    Guid? ScheduledEventId,
    string Customer,
    string? Address,
    string? Carrier,
    string? VehiclePlate,
    string? Reference,
    string? Notes,
    string UserName,
    DateTimeOffset CreatedAt,
    int PalletCount,
    IReadOnlyList<DeliveryNoteLineDto> Lines);

/// <summary>
/// What a new note for a loading slot starts with: the customer and the goods that actually left,
/// with their lots. <see cref="ExistingId"/> is set when the slot already has its note.
/// </summary>
public sealed record DeliveryNoteDraftDto(
    Guid ScheduledEventId,
    Guid? ExistingId,
    string Customer,
    string? Reference,
    DateTimeOffset ShippedAt,
    IReadOnlyList<DeliveryNoteLineDto> Lines);

public sealed class DeliveryNoteInput
{
    /// <summary>The completed loading slot the note belongs to, if any.</summary>
    public Guid? ScheduledEventId { get; set; }

    [Required(ErrorMessage = "Indica el cliente.")]
    [StringLength(120, ErrorMessage = "El cliente no puede pasar de 120 caracteres.")]
    public string Customer { get; set; } = "";

    [StringLength(300, ErrorMessage = "La dirección no puede pasar de 300 caracteres.")]
    public string? Address { get; set; }

    [StringLength(120, ErrorMessage = "El transportista no puede pasar de 120 caracteres.")]
    public string? Carrier { get; set; }

    [StringLength(20, ErrorMessage = "La matrícula no puede pasar de 20 caracteres.")]
    public string? VehiclePlate { get; set; }

    [StringLength(40, ErrorMessage = "La referencia no puede pasar de 40 caracteres.")]
    public string? Reference { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden pasar de 500 caracteres.")]
    public string? Notes { get; set; }

    public List<DeliveryNoteLineInput> Lines { get; set; } = [];
}

public sealed class DeliveryNoteLineInput
{
    public Guid? ProductId { get; set; }

    [StringLength(40, ErrorMessage = "El lote no puede pasar de 40 caracteres.")]
    public string? Lot { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "La cantidad está fuera de rango.")]
    public decimal Quantity { get; set; }

    /// <summary>Pallet number, from 1. Gaps are closed when the note is saved.</summary>
    public int Pallet { get; set; } = 1;
}
