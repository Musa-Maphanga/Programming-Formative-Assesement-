using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RaceDay.Api.Services;

/// <summary>Settings from the "Jwt" section of appsettings.json.</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "RaceDay.Api";
    public string Audience { get; set; } = "RaceDay.Clients";

    /// <summary>Signing key. Must be at least 32 characters. Use user-secrets or an environment variable outside development.</summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 120;
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string fullName, string role);
}

/// <summary>
/// Creates the signed JWT that acts as the user's session. The token carries the user's id and role,
/// is sent with every request, and is checked by the JwtBearer middleware before any controller runs.
/// When it expires the user has to log in again.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string fullName, string role)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
