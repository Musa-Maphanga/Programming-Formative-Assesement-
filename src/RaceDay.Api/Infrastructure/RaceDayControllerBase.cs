using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Infrastructure;

// Shared helpers for all RaceDay controllers
[ApiController]
[Produces("application/json")]
public abstract class RaceDayControllerBase : ControllerBase
{
    protected readonly RaceDayDbContext Db;

    protected RaceDayControllerBase(RaceDayDbContext db)
    {
        Db = db;
    }

    // The logged-in user's server-side session (null on public endpoints)
    protected SessionInfo? CurrentSession => HttpContext.Items[nameof(SessionInfo)] as SessionInfo;

    // The logged-in user's id, taken from the session
    protected int CurrentUserId =>
        CurrentSession?.UserId
        ?? (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0);

    // Loads the logged-in user and checks their role in the database.
    // [Authorize(Roles = ...)] only checks the token. The foreign keys can't stop a Participant's id
    // ending up in Events.OrganiserId, so the stored role is checked here (Part 1 feedback).
    // Returns 401 if the account is gone, 403 if the role is wrong.
    protected async Task<(User? User, ActionResult? Error)> RequireUserInRoleAsync(string requiredRole)
    {
        var user = await Db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
            return (null, ApiError(StatusCodes.Status401Unauthorized, "Your account could not be found. Please log in again."));

        if (!string.Equals(user.Role.RoleName, requiredRole, StringComparison.Ordinal))
            return (null, ApiError(StatusCodes.Status403Forbidden, $"Only a user with the {requiredRole} role can do this."));

        return (user, null);
    }

    // Error response in the standard ProblemDetails format
    protected ObjectResult ApiError(int statusCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                409 => "Conflict",
                502 => "Bad Gateway",
                _ => "Error"
            },
            Detail = detail
        };
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    protected ObjectResult NotOwner(string what = "events") =>
        ApiError(StatusCodes.Status403Forbidden, $"You can only manage your own {what}.");

    // 400 with a field error, same shape as the automatic validation errors
    protected ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    protected static string FullName(User u) => $"{u.FirstName} {u.LastName}";
}