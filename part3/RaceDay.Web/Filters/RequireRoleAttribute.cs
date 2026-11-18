using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Guards a controller or action. An anonymous visitor is sent to the sign-in
/// page with a return address; a signed-in user holding the wrong role is sent
/// to a page that explains why, rather than being bounced back to login as
/// though they were not signed in at all.
///
/// Applying this as a filter rather than checking inside each action means a
/// protected page cannot be left unprotected by forgetting a line of code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    /// <summary>Any signed-in user when no role is named.</summary>
    public RequireRoleAttribute(params string[] roles) => _roles = roles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        if (!session.IsSignedIn())
        {
            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            context.Result = new RedirectToActionResult(
                "Login", "Account", new { returnUrl });
            return;
        }

        if (_roles.Length == 0) return;

        var role = session.GetRole() ?? "";
        var permitted = _roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        if (!permitted)
        {
            context.Result = new RedirectToActionResult("Denied", "Account", null);
        }
    }
}
