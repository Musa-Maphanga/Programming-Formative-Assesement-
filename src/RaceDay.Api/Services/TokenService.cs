using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RaceDay.Api.Services;

// Values from the "Jwt" section in appsettings.json
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "RaceDay.Api";
    public string Audience { get; set; } = "RaceDay.Clients";
    public string Key { get; set; } = string.Empty; // at least 32 characters
    public int ExpiryMinutes { get; set; } = 120;
}

// Custom claim names used in RaceDay tokens
public static class RaceDayClaims
{
    public const string SessionId = "session_id";
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string fullName, string role, string sessionId, DateTime expiresAt);
}

// Builds the signed JWT returned at login. It carries the user id, role and session id.
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(int userId, string email, string fullName, string role, string sessionId, DateTime expiresAt)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Role, role),
            new(RaceDayClaims.SessionId, sessionId),
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