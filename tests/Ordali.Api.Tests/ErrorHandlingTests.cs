using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ordali.Api.Common;

namespace Ordali.Api.Tests;

public class ErrorHandlingTests(OrdaliApiFactory factory) : IClassFixture<OrdaliApiFactory>
{
    [Fact]
    public async Task Broken_business_rules_keep_the_cors_headers_for_the_web_client()
    {
        var client = await factory.SignInRawAsync(OrdaliApiFactory.OperatorEmail);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/movements")
        {
            Content = JsonContent.Create(new
            {
                type = "Shipment",
                productId = DemoIds.Product(DemoIds.Screws),
                fromLocationId = DemoIds.Location(DemoIds.Picking2),
                quantity = 1000,
            }),
        };
        request.Headers.Add("Origin", "http://localhost:8081");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("http://localhost:8081", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Concurrent_writes_are_answered_with_409()
    {
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        context.Response.Body = new MemoryStream();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new DbUpdateConcurrencyException("stale"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task Other_database_errors_are_left_to_the_default_handler()
    {
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, new DbUpdateException("String or binary data would be truncated"), CancellationToken.None);

        Assert.False(handled);
    }
}
