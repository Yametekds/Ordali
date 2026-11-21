namespace Ordali.Api.Common;

/// <summary>
/// A request that breaks a business rule, with the HTTP status and the message for the user.
/// <see cref="ApiExceptionHandler"/> turns it into ProblemDetails, or ValidationProblemDetails
/// when it carries field errors, so endpoints don't need their own try/catch.
/// </summary>
public sealed class RequestRejectedException(int status, string message, IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int Status { get; } = status;

    public IDictionary<string, string[]>? Errors { get; } = errors;
}
