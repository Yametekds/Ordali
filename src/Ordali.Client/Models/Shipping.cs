using System.ComponentModel.DataAnnotations;

namespace Ordali.Client.Models;

public sealed record DeliveryNoteLine(
    Guid ProductId,
    string Sku,
    string ProductName,
    string Unit,
    string? Gtin,
    string? Lot,
    decimal Quantity,
    int Pallet);

/// <summary>The delivery note of an outbound load. Every line says which pallet it travels on.</summary>
public sealed record DeliveryNote(
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
    IReadOnlyList<DeliveryNoteLine> Lines)
{
    /// <summary>The lines of each pallet, from pallet 1.</summary>
    public IEnumerable<IGrouping<int, DeliveryNoteLine>> Pallets => Lines.GroupBy(l => l.Pallet).OrderBy(g => g.Key);
}

/// <summary>What a note for a recorded load starts with. <see cref="ExistingId"/> is set when the load already has one.</summary>
public sealed record DeliveryNoteDraft(
    Guid ScheduledEventId,
    Guid? ExistingId,
    string Customer,
    string? Reference,
    DateTimeOffset ShippedAt,
    IReadOnlyList<DeliveryNoteLine> Lines);

public sealed class DeliveryNoteInput
{
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

    public string? Lot { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Pallet number, from 1. Gaps are closed when the note is saved.</summary>
    public int Pallet { get; set; } = 1;
}
