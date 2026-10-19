using System.Text.RegularExpressions;

namespace RaceDay.Api.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>Returns an error message if the password is too weak, otherwise null.</summary>
    string? CheckStrength(string password);
}

/// <summary>
/// Hashes passwords with BCrypt. BCrypt adds a random salt to every hash and is slow on purpose,
/// so the plain password is never stored and stolen hashes are hard to crack.
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A badly formed hash in the database should fail the login, not crash the API
            return false;
        }
    }

    public string? CheckStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return "Password must be at least 8 characters long.";
        if (!Regex.IsMatch(password, "[A-Z]"))
            return "Password must contain at least one uppercase letter.";
        if (!Regex.IsMatch(password, "[a-z]"))
            return "Password must contain at least one lowercase letter.";
        if (!Regex.IsMatch(password, "[0-9]"))
            return "Password must contain at least one number.";
        return null;
    }
}
