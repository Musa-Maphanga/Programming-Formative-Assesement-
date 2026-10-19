namespace RaceDay.Api.Models;

/// <summary>The two roles a user can register as. These match the rows in the Roles table.</summary>
public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static readonly string[] All = { Organiser, Participant };
}

/// <summary>Allowed values for Events.Status (matches CK_Events_Status).</summary>
public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Upcoming, Completed, Cancelled };
}

/// <summary>Allowed values for Enrolments.Status (matches CK_Enrolments_Status).</summary>
public static class EnrolmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Withdrawn = "Withdrawn";
}

/// <summary>Allowed values for Users.Gender (matches CK_Users_Gender).</summary>
public static class Genders
{
    public static readonly string[] All = { "Male", "Female", "Other" };
}
