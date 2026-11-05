using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ordali.Api.Common;
using Ordali.Api.Data;
using Ordali.Api.Identity;
using Ordali.Api.Inventory;

namespace Ordali.Api.Calendar;

public static class CalendarModule
{
    private static readonly TimeSpan LongestRange = TimeSpan.FromDays(100);

    public static void MapCalendarEndpoints(this IEndpointRouteBuilder api)
    {
        var calendar = api.MapGroup("/calendar").WithTags("Calendario");

        calendar.MapGet("/", ListAsync)
            .RequireAuthorization(Permissions.CalendarView)
            .WithSummary("Citas que se solapan con el intervalo [from, to). Sin fechas, de hoy a 35 días vista. Como mucho 100 días.");
        calendar.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(Permissions.CalendarView)
            .WithName("GetScheduledEvent");
        calendar.MapPost("/", CreateAsync)
            .RequireAuthorization(Permissions.CalendarEdit)
            .Validate<ScheduledEventInput>()
            .WithSummary("Programa una carga, una descarga u otra cita. 'repeatWeeks' la repite cada semana. Devuelve todas las creadas.");
        calendar.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(Permissions.CalendarEdit)
            .Validate<ScheduledEventInput>()
            .WithSummary("Cambia una cita que aún no está hecha ni anulada.");
        calendar.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization(Permissions.CalendarEdit)
            .WithSummary("Anula la cita. Queda en el calendario tachada.");
        calendar.MapPost("/{id:guid}/complete", CompleteAsync)
            .RequireAuthorization(Permissions.CalendarEdit)
            .WithSummary("Marca como hecha una cita que no mueve stock (tipo Other).");
        calendar.MapPost("/{id:guid}/receive", ReceiveAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<ReceiptInput>()
            .WithSummary("Registra la entrada de una descarga. La mercancía queda en recepción para ubicarla.");
        calendar.MapPost("/{id:guid}/ship", ShipAsync)
            .RequireAuthorization(Permissions.MovementsCreate)
            .Validate<ShipEventInput>()
            .WithSummary("Registra la salida de la mercancía de una carga, línea a línea desde su ubicación.");
    }

    private static async Task<IResult> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, OrdaliDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var start = from ?? new DateTimeOffset(time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        var end = to ?? start.AddDays(35);
        if (end <= start)
        {
            return Validation.Invalid("to", "El final tiene que ser posterior al inicio.");
        }

        if (end - start > LongestRange)
        {
            return Validation.Invalid("to", "Pide como mucho 100 días de calendario.");
        }

        var events = await db.ScheduledEvents.AsNoTracking()
            .Include(e => e.Lines)
            .Where(e => e.StartsAt < end && e.EndsAt > start)
            .OrderBy(e => e.StartsAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(db, events, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, OrdaliDbContext db, CancellationToken cancellationToken)
    {
        var scheduled = await db.ScheduledEvents.AsNoTracking().Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return scheduled is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "La cita no existe.")
            : Results.Ok((await ToDtosAsync(db, [scheduled], cancellationToken))[0]);
    }

    private static Task<IResult> CreateAsync(
        ScheduledEventInput input, ClaimsPrincipal user, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () =>
        {
            var created = await service.CreateAsync(input, user.Identity!.Name!, cancellationToken);
            return Results.CreatedAtRoute("GetScheduledEvent", new { id = created[0].Id }, await ToDtosAsync(db, created, cancellationToken));
        });

    private static Task<IResult> UpdateAsync(
        Guid id, ScheduledEventInput input, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () => Results.Ok(await OneAsync(db, await service.UpdateAsync(id, input, cancellationToken), cancellationToken)));

    private static Task<IResult> CancelAsync(
        Guid id, ClaimsPrincipal user, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () => Results.Ok(await OneAsync(db, await service.CancelAsync(id, user.Identity!.Name!, cancellationToken), cancellationToken)));

    private static Task<IResult> CompleteAsync(
        Guid id, ClaimsPrincipal user, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () => Results.Ok(await OneAsync(db, await service.CompleteAsync(id, user.Identity!.Name!, cancellationToken), cancellationToken)));

    private static Task<IResult> ReceiveAsync(
        Guid id, ReceiptInput input, ClaimsPrincipal user, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () =>
        {
            var (scheduled, _) = await service.ReceiveAsync(id, input, user.UserId(), user.Identity!.Name!, cancellationToken);
            return Results.Ok(await OneAsync(db, scheduled, cancellationToken));
        });

    private static Task<IResult> ShipAsync(
        Guid id, ShipEventInput input, ClaimsPrincipal user, CalendarService service, OrdaliDbContext db, CancellationToken cancellationToken) =>
        HandleAsync(async () =>
            Results.Ok(await OneAsync(db, await service.ShipAsync(id, input, user.UserId(), user.Identity!.Name!, cancellationToken), cancellationToken)));

    private static async Task<IResult> HandleAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MovementRejectedException ex) when (ex.Errors is not null)
        {
            return Results.ValidationProblem(ex.Errors, detail: ex.Message, title: "Revisa los datos enviados.");
        }
        catch (MovementRejectedException ex)
        {
            return Results.Problem(statusCode: ex.Status, detail: ex.Message);
        }
    }

    private static async Task<ScheduledEventDto> OneAsync(OrdaliDbContext db, ScheduledEvent scheduled, CancellationToken cancellationToken) =>
        (await ToDtosAsync(db, [scheduled], cancellationToken))[0];

    private static async Task<List<ScheduledEventDto>> ToDtosAsync(
        OrdaliDbContext db, IReadOnlyList<ScheduledEvent> events, CancellationToken cancellationToken)
    {
        var productIds = events.SelectMany(e => e.Lines).Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
        var dockIds = events.Where(e => e.DockLocationId != null).Select(e => e.DockLocationId!.Value).Distinct().ToList();
        var docks = await db.Locations.AsNoTracking().Where(l => dockIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);

        return [.. events.Select(e => new ScheduledEventDto(
            e.Id,
            e.Type,
            e.Status,
            e.Title,
            e.Partner,
            e.StartsAt,
            e.EndsAt,
            e.DockLocationId,
            e.DockLocationId is { } d ? docks.GetValueOrDefault(d) : null,
            e.Notes,
            e.SeriesId,
            e.CreatedByName,
            e.CreatedAt,
            e.CompletedByName,
            e.CompletedAt,
            e.Outcome,
            [.. e.Lines
                .Select(l => products[l.ProductId])
                .Zip(e.Lines)
                .OrderBy(x => x.First.Sku, StringComparer.Ordinal)
                .Select(x => new ScheduledEventLineDto(x.First.Id, x.First.Sku, x.First.Name, x.First.Unit, x.First.TracksLots, x.Second.Quantity))]))];
    }
}
