using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

// Register, log in and log out
[Route("api/auth")]
[Tags("Auth")]
public class AuthController : RaceDayControllerBase
{
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IUserSessionStore _sessions;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher, ITokenService tokens, IUserSessionStore sessions) : base(db)
    {
        _hasher = hasher;
        _tokens = tokens;
        _sessions = sessions;
    }

    /// <summary>Lets a new user sign up as either an Organiser or a Participant.</summary>
    /// <response code="201">The new user's details (without the password).</response>
    /// <response code="400">Missing or invalid fields, an unknown role, or a weak password.</response>
    /// <response code="409">The email is already registered.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserProfileResponse>> Register(RegisterRequest request)
    {
        // Only "Organiser" or "Participant" (any capitalisation) - nobody can sign up as e.g. "Admin"
        var roleName = RoleNames.All.FirstOrDefault(r => string.Equals(r, request.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (roleName is null)
            return FieldError(nameof(request.Role), "Role must be either 'Organiser' or 'Participant'.");

        if (request.Gender is not null && !Genders.All.Contains(request.Gender))
            return FieldError(nameof(request.Gender), "Gender must be Male, Female or Other.");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow))
            return FieldError(nameof(request.DateOfBirth), "Date of birth cannot be in the future.");

        var weakPassword = _hasher.CheckStrength(request.Password);
        if (weakPassword is not null)
            return FieldError(nameof(request.Password), weakPassword);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await Db.Users.AnyAsync(u => u.Email == email))
            return ApiError(StatusCodes.Status409Conflict, "An account with this email already exists.");

        var role = await Db.Roles.SingleAsync(r => r.RoleName == roleName);

        var user = new User
        {
            RoleId = role.RoleId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password), // never store the plain password
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            City = request.City,
            CreatedAt = DateTime.UtcNow
        };

        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        user.Role = role;

        return CreatedAtAction(nameof(UsersController.GetMe), "Users", null, UsersController.ToProfile(user));
    }

    /// <summary>Checks the user's email and password and gives back a token so they can use the system.</summary>
    /// <response code="200">{ token, expiresAt, userId, role }</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">The email or password is wrong.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await Db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        // Same message either way so we don't reveal which emails exist
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return ApiError(StatusCodes.Status401Unauthorized, "The email or password is incorrect.");

        // Start a server-side session, then give back a token that points to it
        var session = await _sessions.CreateAsync(user.UserId, user.Role.RoleName);
        var (token, expiresAt) = _tokens.CreateToken(user.UserId, user.Email, FullName(user), user.Role.RoleName,
            session.SessionId, session.ExpiresAt);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.UserId,
            Role = user.Role.RoleName
        });
    }

    /// <summary>Logs the user out by ending their server-side session.</summary>
    /// <response code="204">Logged out.</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        if (CurrentSession is not null)
            await _sessions.RemoveAsync(CurrentSession.SessionId);
        return NoContent();
    }
}