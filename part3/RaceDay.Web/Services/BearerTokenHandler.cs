using System.Net.Http.Headers;

namespace RaceDay.Web.Services;

/// <summary>
/// Attaches the signed-in user's JWT to every outgoing API request. Doing it
/// in a handler rather than in each call means no controller can forget it,
/// and no controller needs to know the token exists.
/// </summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public BearerTokenHandler(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.Session.GetToken();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
