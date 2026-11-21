using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Ordali.Api.Common;

public static class Validation
{
    public const string Title = "Revisa los datos enviados.";

    /// <summary>
    /// Runs the data annotations of the body, including the items of its lists (receipt lines,
    /// shipment lines...), and answers 400 with ValidationProblemDetails.
    /// </summary>
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder)
        where T : class =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var body = context.Arguments.OfType<T>().FirstOrDefault();
            if (body is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Falta el cuerpo de la petición.");
            }

            var errors = Errors(body);
            return errors.Count > 0 ? Invalid(errors) : await next(context);
        });

    /// <summary>
    /// Field errors keyed by camelCase property name. An error in a list item goes under the
    /// list's key as "Línea N: ...", the same shape the services use for line errors.
    /// </summary>
    public static Dictionary<string, string[]> Errors(object body)
    {
        var errors = new List<(string Key, string Message)>();
        Collect(body, errors);
        return errors
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).Distinct().ToArray());
    }

    public static IResult Invalid(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: Title);

    public static IResult Invalid(string member, string message) =>
        Invalid(new Dictionary<string, string[]> { [Key(member)] = [message] });

    private static void Collect(object value, List<(string Key, string Message)> errors)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        foreach (var result in results)
        {
            foreach (var member in result.MemberNames)
            {
                errors.Add((Key(member), result.ErrorMessage ?? "Valor no válido."));
            }
        }

        foreach (var property in ItemLists(value.GetType()))
        {
            if (property.GetValue(value) is not IEnumerable items)
            {
                continue;
            }

            var number = 0;
            foreach (var item in items)
            {
                number++;
                if (item is null)
                {
                    continue;
                }

                var itemErrors = new List<(string Key, string Message)>();
                Collect(item, itemErrors);
                var prefix = string.Format(CultureInfo.InvariantCulture, "Línea {0}: ", number);
                errors.AddRange(itemErrors.Select(e => (Key(property.Name), prefix + LowerFirst(e.Message))));
            }
        }
    }

    // Lists of request objects, such as List<ReceiptLineInput>. Lists of strings or Guids have nothing to validate inside.
    private static IEnumerable<PropertyInfo> ItemLists(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType != typeof(string)
                && p.PropertyType.IsGenericType
                && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                && p.PropertyType.GetGenericArguments() is [{ IsClass: true } item]
                && item != typeof(string));

    private static string LowerFirst(string text) =>
        text.Length == 0 ? text : char.ToLower(text[0], CultureInfo.GetCultureInfo("es-ES")) + text[1..];

    private static string Key(string member) => JsonNamingPolicy.CamelCase.ConvertName(member);
}
