using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>2. User Profile – view and update your own details.</summary>
[Route("api/users/me")]
[Authorize]
[Tags("2. User Profile")]
public class UsersController : RaceDayControllerBase
{
    private readonly IPasswordHasher _hasher;

    public UsersController(RaceDayDbContext db, IPasswordHasher hasher) : base(db)
    {
        _hasher = hasher;
    }

    /// <summary>Shows the profile of the user who is logged in.</summary>
    /// <remarks>The user is worked out from the token, so a user can only ever see their own profile.</remarks>
    /// <response code="200">The user's profile.</response>
    /// <response code="404">The user no longer exists.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var user = await Db.Users.Include(u => u.Role).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
            return ApiError(StatusCodes.Status404NotFound, "This user no longer exists.");

        return Ok(ToProfile(user));
    }

    /// <summary>Lets the logged-in user update their own profile details.</summary>
    /// <remarks>Email and role can't be changed here. Every field in the body replaces the stored value.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Invalid fields.</response>
    /// <response code="404">The user no longer exists.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> UpdateMe(UpdateProfileRequest request)
    {
        if (request.Gender is not null && !Genders.All.Contains(request.Gender))
            return FieldError(nameof(request.Gender), "Gender must be Male, Female or Other.");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow))
            return FieldError(nameof(request.DateOfBirth), "Date of birth cannot be in the future.");

        var user = await Db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
            return ApiError(StatusCodes.Status404NotFound, "This user no longer exists.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber;
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender;
        user.City = request.City;
        await Db.SaveChangesAsync();

        return Ok(ToProfile(user));
    }

    /// <summary>Lets the user change their password, but only if they enter their current password first.</summary>
    /// <response code="204">Password changed.</response>
    /// <response code="400">The new password is too weak.</response>
    /// <response code="401">The current password is wrong.</response>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await Db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
            return ApiError(StatusCodes.Status401Unauthorized, "Your account could not be found. Please log in again.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return ApiError(StatusCodes.Status401Unauthorized, "The current password is incorrect.");

        var weakPassword = _hasher.CheckStrength(request.NewPassword);
        if (weakPassword is not null)
            return FieldError(nameof(request.NewPassword), weakPassword);

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    internal static UserProfileResponse ToProfile(User u) => new()
    {
        UserId = u.UserId,
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        Role = u.Role.RoleName,
        PhoneNumber = u.PhoneNumber,
        DateOfBirth = u.DateOfBirth,
        Gender = u.Gender,
        City = u.City,
        CreatedAt = u.CreatedAt
    };
}
