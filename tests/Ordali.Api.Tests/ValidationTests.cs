using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ordali.Api.Tests;

public class ValidationTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Receipt_line_with_a_lot_too_long_is_a_field_error_not_a_stock_conflict()
    {
        var client = await factory.SignInRawAsync(OrdaliApiFactory.OperatorEmail);

        var response = await client.PostAsJsonAsync("/api/v1/receipts", new
        {
            lines = new[] { new { productId = DemoIds.Product(DemoIds.Rice), lot = new string('L', 41), quantity = 5 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal(["Línea 1: el lote no puede pasar de 40 caracteres."], errors["lines"]);
    }

    [Fact]
    public async Task Every_invalid_line_is_reported_with_its_number()
    {
        var client = await factory.SignInRawAsync(OrdaliApiFactory.SupervisorEmail);
        var start = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(90).AddHours(8);

        var response = await client.PostAsJsonAsync("/api/v1/calendar", new
        {
            type = "Inbound",
            title = "Proveedor de prueba",
            startsAt = start,
            endsAt = start.AddHours(1),
            lines = new[]
            {
                new { productId = DemoIds.Product(DemoIds.Rice), quantity = 10m },
                new { productId = DemoIds.Product(DemoIds.Tape), quantity = 2_000_000m },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorsAsync(response);
        Assert.Equal(["Línea 2: la cantidad está fuera de rango."], errors["lines"]);
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(OrdaliApiFactory.Json);
        return body.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(OrdaliApiFactory.Json)!;
    }
}
