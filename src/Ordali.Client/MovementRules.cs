using Ordali.Client.Models;

namespace Ordali.Client;

/// <summary>
/// Which locations and quantities each movement type needs. The forms use it to show
/// the right fields and the demo backend uses it to validate, like the API does.
/// </summary>
public static class MovementRules
{
    public static bool NeedsFrom(MovementType type) => type is MovementType.Shipment or MovementType.Transfer;

    public static bool NeedsTo(MovementType type) => type is MovementType.Receipt or MovementType.Transfer or MovementType.Adjustment;

    public static Dictionary<string, string[]> Validate(MovementInput input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.ProductId is null)
        {
            errors[nameof(MovementInput.ProductId)] = ["Elige un artículo."];
        }

        if (NeedsFrom(input.Type) && input.FromLocationId is null)
        {
            errors[nameof(MovementInput.FromLocationId)] = ["Elige la ubicación de origen."];
        }

        if (NeedsTo(input.Type) && input.ToLocationId is null)
        {
            errors[nameof(MovementInput.ToLocationId)] = [input.Type == MovementType.Adjustment ? "Elige la ubicación." : "Elige la ubicación de destino."];
        }

        if (input.Type == MovementType.Transfer && input.FromLocationId is not null && input.FromLocationId == input.ToLocationId)
        {
            errors[nameof(MovementInput.ToLocationId)] = ["El destino tiene que ser distinto del origen."];
        }

        if (input.Type == MovementType.Adjustment ? input.Quantity == 0 : input.Quantity <= 0)
        {
            errors[nameof(MovementInput.Quantity)] = [input.Type == MovementType.Adjustment
                ? "El ajuste no puede ser cero."
                : "La cantidad tiene que ser mayor que cero."];
        }

        if (input.Type == MovementType.Adjustment && string.IsNullOrWhiteSpace(input.Reason))
        {
            errors[nameof(MovementInput.Reason)] = ["Indica el motivo del ajuste."];
        }

        return errors;
    }
}
