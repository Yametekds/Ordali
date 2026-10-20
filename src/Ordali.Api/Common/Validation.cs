using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Ordali.Api.Common;

public static class Validation
{
    /// <summary>Runs the data annotations of the body and answers 400 with ValidationProblemDetails.</summary>
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var body = context.Arguments.OfType<T>().FirstOrDefault();
            if (body is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Falta el cuerpo de la petición.");
            }

            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(body, new ValidationContext(body), results, validateAllProperties: true))
            {
                var errors = results
                    .SelectMany(r => r.MemberNames.Select(member => (Member: Key(member), r.ErrorMessage)))
                    .GroupBy(e => e.Member)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "Valor no válido.").ToArray());
                return Invalid(errors);
            }

            return await next(context);
        });

    public static IResult Invalid(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: "Revisa los datos enviados.");

    public static IResult Invalid(string member, string message) =>
        Invalid(new Dictionary<string, string[]> { [Key(member)] = [message] });

    private static string Key(string member) => JsonNamingPolicy.CamelCase.ConvertName(member);
}
