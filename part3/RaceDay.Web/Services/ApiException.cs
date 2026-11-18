using System.Net;

namespace RaceDay.Web.Services;

/// <summary>
/// Raised when the Part 2 API answers with a failure status. It carries the
/// status code and a message fit to show a user, so controllers can turn an
/// API refusal into a form error rather than letting an exception reach the
/// browser as a stack trace.
/// </summary>
public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public IReadOnlyList<string> Details { get; }

    public ApiException(HttpStatusCode statusCode, string message, IReadOnlyList<string>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        Details = details ?? Array.Empty<string>();
    }

    public bool IsUnauthorised => StatusCode is HttpStatusCode.Unauthorized;
    public bool IsForbidden => StatusCode is HttpStatusCode.Forbidden;
    public bool IsNotFound => StatusCode is HttpStatusCode.NotFound;

    /// <summary>
    /// A sentence safe to put in front of a user. The API's own message is
    /// preferred where it gave one, since it knows which rule was broken.
    /// </summary>
    public string UserMessage => StatusCode switch
    {
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
        HttpStatusCode.Forbidden => "You do not have permission to do that.",
        HttpStatusCode.NotFound => "We could not find what you were looking for.",
        HttpStatusCode.ServiceUnavailable => "That service is temporarily unavailable. Please try again shortly.",
        _ => string.IsNullOrWhiteSpace(Message)
            ? "Something went wrong while talking to the RaceDay service."
            : Message
    };
}
