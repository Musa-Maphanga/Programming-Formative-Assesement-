using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;

namespace RaceDay.Api.Infrastructure;

/// <summary>
/// Shared helpers for the RaceDay controllers: reading the logged-in user from the token,
/// checking their role against the database, and returning consistent error bodies.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class RaceDayControllerBase : ControllerBase
{
    protected readonly RaceDayDbContext Db;

    protected RaceDayControllerBase(RaceDayDbContext db)
    {
        Db = db;
    }

    /// <summary>The UserId stored in the token (the NameIdentifier claim).</summary>
    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>
    /// Loads the logged-in user and confirms that their role in the database is the one required.
    /// The [Authorize(Roles = ...)] attribute already checks the role inside the token, but the
    /// Users.RoleId foreign key cannot stop, say, a Participant's id ending up in Events.OrganiserId.
    /// Checking the stored role here is what enforces that rule (Part 1 feedback).
    /// Returns the user, or an error result (401 if the account no longer exists, 403 if the role is wrong).
    /// </summary>
    protected async Task<(User? User, ActionResult? Error)> RequireUserInRoleAsync(string requiredRole)
    {
        var user = await Db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
            return (null, ApiError(StatusCodes.Status401Unauthorized, "Your account could not be found. Please log in again."));

        if (!string.Equals(user.Role.RoleName, requiredRole, StringComparison.Ordinal))
            return (null, ApiError(StatusCodes.Status403Forbidden, $"Only a user with the {requiredRole} role can do this."));

        return (user, null);
    }

    /// <summary>Returns a ProblemDetails body with the given status code and message.</summary>
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

    /// <summary>Adds a field error and returns a 400 in the same shape as the automatic model validation.</summary>
    protected ActionResult FieldError(string field, string message)
    {
        ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }

    protected static string FullName(User u) => $"{u.FirstName} {u.LastName}";
}
