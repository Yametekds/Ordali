using Ordali.Api.Common;
using Ordali.Api.Data;

namespace Ordali.Api.Inventory;

/// <summary>Which locations and quantities each movement type needs.</summary>
public static class MovementRules
{
    public static bool NeedsFrom(MovementType type) => type is MovementType.Shipment or MovementType.Transfer;

    public static bool NeedsTo(MovementType type) => type is MovementType.Receipt or MovementType.Transfer or MovementType.Adjustment;

    public static Dictionary<string, string[]> Validate(MovementInput input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.ProductId is null)
        {
            errors["productId"] = ["Elige un artículo."];
        }

        if (NeedsFrom(input.Type) && input.FromLocationId is null)
        {
            errors["fromLocationId"] = ["Elige la ubicación de origen."];
        }

        if (NeedsTo(input.Type) && input.ToLocationId is null)
        {
            errors["toLocationId"] = [input.Type == MovementType.Adjustment ? "Elige la ubicación." : "Elige la ubicación de destino."];
        }

        if (input.Type == MovementType.Transfer && input.FromLocationId is not null && input.FromLocationId == input.ToLocationId)
        {
            errors["toLocationId"] = ["El destino tiene que ser distinto del origen."];
        }

        if (input.Type == MovementType.Adjustment ? input.Quantity == 0 : input.Quantity <= 0)
        {
            errors["quantity"] = [input.Type == MovementType.Adjustment
                ? "El ajuste no puede ser cero."
                : "La cantidad tiene que ser mayor que cero."];
        }

        if (input.Type == MovementType.Adjustment && string.IsNullOrWhiteSpace(input.Reason))
        {
            errors["reason"] = ["Indica el motivo del ajuste."];
        }

        return errors;
    }
}
