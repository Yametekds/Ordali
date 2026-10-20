using System.Net;

namespace Ordali.Client;

/// <summary>
/// An error returned by the API as ProblemDetails, or raised by the demo backend with the same shape.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException()
        : this(HttpStatusCode.InternalServerError, "Error inesperado.")
    {
    }

    public ApiException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    public ApiException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, innerException)
    {
    }

    public ApiException(
        HttpStatusCode status,
        string message,
        IReadOnlyDictionary<string, string[]>? errors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Status = status;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public HttpStatusCode Status { get; }

    /// <summary>Validation messages per field, as in ValidationProblemDetails.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
