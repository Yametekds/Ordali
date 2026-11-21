using System.ComponentModel.DataAnnotations;

namespace Ordali.Client.Models;

public enum ScheduledEventType
{
    /// <summary>Unloading: the goods come in as a receipt.</summary>
    Inbound,

    /// <summary>Loading: the goods leave as shipments.</summary>
    Outbound,

    /// <summary>Anything else, such as a stock count or a visit.</summary>
    Other,
}

public enum ScheduledEventStatus
{
    Scheduled,
    Completed,
    Cancelled,
}

public sealed record ScheduledEventLine(Guid ProductId, string Sku, string ProductName, string Unit, bool TracksLots, decimal Quantity);

/// <summary>An entry of the warehouse calendar: a loading or unloading slot, or another appointment.</summary>
public sealed record ScheduledEvent(
    Guid Id,
    ScheduledEventType Type,
    ScheduledEventStatus Status,
    string Title,
    string? Partner,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid? DockLocationId,
    string? DockLocationCode,
    string? Notes,
    Guid? SeriesId,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    string? CompletedByName,
    DateTimeOffset? CompletedAt,
    string? Outcome,
    IReadOnlyList<ScheduledEventLine> Lines)
{
    /// <summary>Still open after the time it should have finished.</summary>
    public bool IsLate(DateTimeOffset now) => Status == ScheduledEventStatus.Scheduled && EndsAt < now;
}

public sealed class ScheduledEventInput
{
    public ScheduledEventType Type { get; set; } = ScheduledEventType.Inbound;

    [Required(ErrorMessage = "Escribe un título.")]
    [StringLength(120, ErrorMessage = "El título no puede pasar de 120 caracteres.")]
    public string Title { get; set; } = "";

    [StringLength(120, ErrorMessage = "El proveedor o cliente no puede pasar de 120 caracteres.")]
    public string? Partner { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public Guid? DockLocationId { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden pasar de 500 caracteres.")]
    public string? Notes { get; set; }

    public List<ScheduledEventLineInput> Lines { get; set; } = [];

    /// <summary>Only on creation: how many more weeks to repeat it.</summary>
    [Range(0, 26, ErrorMessage = "Se puede repetir como mucho 26 semanas.")]
    public int RepeatWeeks { get; set; }

    /// <summary>Set after the user has seen the warning that the dock is already booked.</summary>
    public bool AllowOverlap { get; set; }

    public static ScheduledEventInput From(ScheduledEvent scheduled) => new()
    {
        Type = scheduled.Type,
        Title = scheduled.Title,
        Partner = scheduled.Partner,
        StartsAt = scheduled.StartsAt,
        EndsAt = scheduled.EndsAt,
        DockLocationId = scheduled.DockLocationId,
        Notes = scheduled.Notes,
        Lines = [.. scheduled.Lines.Select(l => new ScheduledEventLineInput { ProductId = l.ProductId, Quantity = l.Quantity })],
    };
}

public sealed class ScheduledEventLineInput
{
    public Guid? ProductId { get; set; }

    public decimal Quantity { get; set; }
}

public sealed class ShipEventInput
{
    /// <summary>Order or delivery note; left empty, the event title is used.</summary>
    [StringLength(40, ErrorMessage = "La referencia no puede pasar de 40 caracteres.")]
    public string? Reference { get; set; }

    public List<ShipLineInput> Lines { get; set; } = [];
}

public sealed class ShipLineInput
{
    public Guid? ProductId { get; set; }

    public Guid? FromLocationId { get; set; }

    public string? Lot { get; set; }

    public decimal Quantity { get; set; }
}
