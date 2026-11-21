using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Ordali.Api.Common;

/// <summary>
/// Answers the exceptions that are part of normal operation: broken business rules and two
/// people saving the same thing at once. Anything else falls through to the default handler,
/// which logs it and returns a 500 ProblemDetails.
/// </summary>
public sealed partial class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var result = exception switch
        {
            RequestRejectedException { Errors: not null } rejected =>
                Results.ValidationProblem(rejected.Errors, detail: rejected.Message, title: Validation.Title),
            RequestRejectedException rejected =>
                Results.Problem(statusCode: rejected.Status, detail: rejected.Message),
            DbUpdateException update when DatabaseErrors.IsWriteConflict(update) =>
                Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: "Otra persona ha guardado un cambio sobre los mismos datos a la vez. Vuelve a intentarlo."),
            _ => null,
        };

        if (result is null)
        {
            return false;
        }

        if (exception is DbUpdateException)
        {
            LogWriteConflict(logger, httpContext.Request.Method, httpContext.Request.Path);
        }

        await result.ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Write conflict on {Method} {Path}: answered 409.")]
    private static partial void LogWriteConflict(ILogger logger, string method, string path);
}
