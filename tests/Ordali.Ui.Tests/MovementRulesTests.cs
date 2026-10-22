using Ordali.Client;
using Ordali.Client.Models;

namespace Ordali.Ui.Tests;

public class MovementRulesTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [Fact]
    public void Valid_transfer_has_no_errors()
    {
        var input = new MovementInput { Type = MovementType.Transfer, ProductId = A, FromLocationId = A, ToLocationId = B, Quantity = 1 };

        Assert.Empty(MovementRules.Validate(input));
    }

    [Fact]
    public void Transfer_to_the_same_location_is_invalid()
    {
        var input = new MovementInput { Type = MovementType.Transfer, ProductId = A, FromLocationId = B, ToLocationId = B, Quantity = 1 };

        Assert.Contains(nameof(MovementInput.ToLocationId), MovementRules.Validate(input).Keys);
    }

    [Theory]
    [InlineData(MovementType.Receipt, 0)]
    [InlineData(MovementType.Shipment, -3)]
    [InlineData(MovementType.Adjustment, 0)]
    public void Quantity_must_make_sense_for_the_type(MovementType type, int quantity)
    {
        var input = new MovementInput { Type = type, ProductId = A, FromLocationId = A, ToLocationId = B, Quantity = quantity, Reason = "x" };

        Assert.Contains(nameof(MovementInput.Quantity), MovementRules.Validate(input).Keys);
    }

    [Fact]
    public void Adjustments_need_a_reason()
    {
        var input = new MovementInput { Type = MovementType.Adjustment, ProductId = A, ToLocationId = B, Quantity = -2 };

        Assert.Contains(nameof(MovementInput.Reason), MovementRules.Validate(input).Keys);
    }
}
