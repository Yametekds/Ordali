using System.Net;
using System.Text;
using Ordali.Client;
using Ordali.Client.Auth;
using Ordali.Client.Http;
using Ordali.Client.Models;

namespace Ordali.Ui.Tests;

public class HttpOrdaliApiTests
{
    private readonly SessionState _session = new(TimeProvider.System);

    [Fact]
    public async Task Sends_the_token_and_reads_enums_as_strings()
    {
        _session.Set(new LoginResponse("abc", DateTimeOffset.UtcNow.AddHours(1), new UserInfo(Guid.NewGuid(), "Ana", "ana@x.es", UserCategory.Operator, [Permissions.MovementsView])));
        HttpRequestMessage? sent = null;
        var api = Create(request =>
        {
            sent = request;
            return Json(HttpStatusCode.OK, """
                [{"id":"9b0b3c1e-0000-0000-0000-000000000001","type":"Transfer","productId":"9b0b3c1e-0000-0000-0000-000000000002",
                  "sku":"A-1","lot":null,"fromLocationCode":"A","toLocationCode":"B","quantity":3,"reason":null,
                  "userName":"Ana","createdAt":"2026-10-19T08:00:00Z"}]
                """);
        });

        var movements = await api.GetMovementsAsync(take: 10);

        Assert.Equal("Bearer abc", sent!.Headers.Authorization!.ToString());
        Assert.Equal("/api/v1/movements?take=10", sent.RequestUri!.PathAndQuery);
        Assert.Equal(MovementType.Transfer, Assert.Single(movements).Type);
    }

    [Fact]
    public async Task Turns_validation_problem_details_into_field_errors()
    {
        var api = Create(_ => Json(HttpStatusCode.BadRequest, """
            {"title":"One or more validation errors occurred.","status":400,"errors":{"sku":["Ya existe."]}}
            """));

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.CreateProductAsync(new ProductInput()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.Status);
        Assert.Equal(["Ya existe."], ex.Errors["sku"]);
    }

    [Fact]
    public async Task Unauthorized_signs_the_user_out()
    {
        _session.Set(new LoginResponse("old", DateTimeOffset.UtcNow.AddHours(1), new UserInfo(Guid.NewGuid(), "Ana", "ana@x.es", UserCategory.Operator, [])));
        var api = Create(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<ApiException>(() => api.GetProductsAsync());

        Assert.Null(_session.Current);
    }

    [Fact]
    public async Task Connection_failures_get_a_readable_message()
    {
        var api = Create(_ => throw new HttpRequestException("refused"));

        var ex = await Assert.ThrowsAsync<ApiException>(() => api.GetSitesAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Status);
    }

    private HttpOrdaliApi Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("http://api.test/") }, _session);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
