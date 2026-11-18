namespace RaceDay.Web.Services;

/// <summary>
/// The session is the only place the web application remembers who is signed in.
/// There is no cookie authentication middleware and no local user store: the
/// Part 2 API issues a JWT, the session holds it, and every outgoing API call
/// attaches it. Signing out clears the session, which is what makes the token
/// unreachable and ends the user's authenticated state.
/// </summary>
public static class SessionKeys
{
    public const string Token = "raceday.token";
    public const string UserId = "raceday.userId";
    public const string Role = "raceday.role";
    public const string DisplayName = "raceday.displayName";
}

public static class SessionExtensions
{
    public static void SignIn(this ISession session, string token, int userId, string role, string displayName)
    {
        session.SetString(SessionKeys.Token, token);
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, role);
        session.SetString(SessionKeys.DisplayName, displayName);
    }

    public static string? GetToken(this ISession session) => session.GetString(SessionKeys.Token);

    public static int? GetUserId(this ISession session) => session.GetInt32(SessionKeys.UserId);

    public static string? GetRole(this ISession session) => session.GetString(SessionKeys.Role);

    public static string GetDisplayName(this ISession session)
        => session.GetString(SessionKeys.DisplayName) ?? "";

    public static bool IsSignedIn(this ISession session)
        => !string.IsNullOrEmpty(session.GetToken());

    public static bool IsInRole(this ISession session, string role)
        => string.Equals(session.GetRole(), role, StringComparison.OrdinalIgnoreCase);

    public static bool IsOrganiser(this ISession session) => session.IsInRole(Roles.Organiser);

    public static bool IsParticipant(this ISession session) => session.IsInRole(Roles.Participant);
}

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}
