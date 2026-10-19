using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Details needed to create an account.</summary>
public class RegisterRequest
{
    /// <example>Musa</example>
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    /// <example>Dlamini</example>
    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    /// <example>musa.dlamini@example.co.za</example>
    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters with an uppercase letter, a lowercase letter and a number.</summary>
    /// <example>Password123!</example>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant". Any other value is rejected.</summary>
    /// <example>Participant</example>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <example>0821234567</example>
    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Needed to check age-limited categories when entering events.</summary>
    /// <example>1999-05-21</example>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Male, Female or Other.</summary>
    /// <example>Male</example>
    public string? Gender { get; set; }

    /// <example>Johannesburg</example>
    [StringLength(100)]
    public string? City { get; set; }
}

/// <summary>Login details.</summary>
public class LoginRequest
{
    /// <example>sipho.mahlangu@gmail.com</example>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Password123!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned after a successful login. Send the token as "Bearer {token}" on every other request.</summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

/// <summary>A user's profile. The password hash is never returned.</summary>
public class UserProfileResponse
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Fields a user may change on their own profile. Email and role cannot be changed here.</summary>
public class UpdateProfileRequest
{
    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Male, Female or Other.</summary>
    public string? Gender { get; set; }

    [StringLength(100)]
    public string? City { get; set; }
}

/// <summary>Current and new password.</summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>At least 8 characters with an uppercase letter, a lowercase letter and a number.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
