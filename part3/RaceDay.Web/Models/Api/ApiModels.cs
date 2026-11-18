namespace RaceDay.Web.Models.Api;

/*
 * ---------------------------------------------------------------------------
 * The Part 2 API contract, mirrored exactly.
 *
 * Property names match the API's own DTOs one for one, so no mapping
 * attributes are needed. The MVC project has no knowledge of SQL Server, no
 * database context and no entity classes: all of this arrives as JSON.
 *
 * If a route or a payload changes in Part 2, this file and
 * Services/RaceDayApiClient.cs are the only two places that follow it.
 * ---------------------------------------------------------------------------
 */

// ----------------------------------------------------------------- identity

/// <summary>What POST /api/auth/login answers with. There is no name in it.</summary>
public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public string Role { get; set; } = "";
}

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>The API accepts only "Organiser" or "Participant".</summary>
    public string Role { get; set; } = "";

    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
}

public class UserProfile
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class UpdateProfileRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
}

// ------------------------------------------------------------------- events

public class EventTypeDto
{
    public int EventTypeId { get; set; }
    public string TypeName { get; set; } = "";
}

public class EventDto
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Date and start time together, as the API stores it.</summary>
    public DateTime EventDate { get; set; }

    public string Location { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public string EventType { get; set; } = "";
    public int? MaxParticipants { get; set; }
    public int EnrolledCount { get; set; }

    /// <summary>Upcoming, Completed or Cancelled.</summary>
    public string Status { get; set; } = EventStatuses.Upcoming;

    public string? BannerImageUrl { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    /// <summary>Only filled by GET /api/events/{id}.</summary>
    public List<CategoryDto> Categories { get; set; } = new();

    public bool HasTakenPlace => EventDate < DateTime.Now;

    public bool IsOpenForEntry =>
        Status == EventStatuses.Upcoming && EventDate > DateTime.Now && !IsFull;

    public bool IsFull => MaxParticipants.HasValue && EnrolledCount >= MaxParticipants.Value;

    public int? PlacesRemaining =>
        MaxParticipants.HasValue ? Math.Max(0, MaxParticipants.Value - EnrolledCount) : null;
}

/// <summary>POST /api/events. The organiser comes from the token, not the body.</summary>
public class CreateEventRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public int? MaxParticipants { get; set; }
}

/// <summary>PUT /api/events/{id}. Same fields as create, plus the status.</summary>
public class UpdateEventRequest : CreateEventRequest
{
    public string Status { get; set; } = EventStatuses.Upcoming;
}

// --------------------------------------------------------------- categories

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }
    public int EnrolledCount { get; set; }

    /// <summary>Reads the age limits back as a phrase, or an empty string when there are none.</summary>
    public string AgeLimitDisplay => (MinAge, MaxAge) switch
    {
        (null, null) => "",
        ({ } min, null) => $"{min} and over",
        (null, { } max) => $"up to {max}",
        var (min, max) => $"{min} to {max}"
    };
}

public class SaveCategoryRequest
{
    public string Name { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }
}

// --------------------------------------------------------------- enrolments

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";

    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public string City { get; set; } = "";
    public string EventStatus { get; set; } = "";

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }

    public string? RaceNumber { get; set; }

    /// <summary>Confirmed or Withdrawn.</summary>
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;

    public DateTime EnrolledAt { get; set; }
    public ResultDto? Result { get; set; }

    public bool IsConfirmed => Status == EnrolmentStatuses.Confirmed;
    public bool IsUpcoming => EventDate >= DateTime.Now && EventStatus != EventStatuses.Cancelled;
}

public class CreateEnrolmentRequest
{
    public int EventId { get; set; }
    public int CategoryId { get; set; }
}

// ------------------------------------------------------------------ results

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";
    public string? RaceNumber { get; set; }

    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public decimal DistanceKm { get; set; }

    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
    public int FinisherCount { get; set; }
    public DateTime RecordedAt { get; set; }

    public string FinishTimeDisplay => FinishTime.ToString(@"hh\:mm\:ss");

    /// <summary>Minutes per kilometre, the figure runners actually compare.</summary>
    public string PaceDisplay
    {
        get
        {
            if (DistanceKm <= 0 || FinishTime <= TimeSpan.Zero) return "-";
            var pace = TimeSpan.FromSeconds(FinishTime.TotalSeconds / (double)DistanceKm);
            return $"{(int)pace.TotalMinutes}:{pace.Seconds:00} /km";
        }
    }

    /// <summary>"118th out of 1204 finishers", as the brief asks for.</summary>
    public string PositionDisplay =>
        FinisherCount > 0
            ? $"{Ordinal(Position)} out of {FinisherCount} finishers"
            : Ordinal(Position);

    private static string Ordinal(int n)
    {
        if (n % 100 is >= 11 and <= 13) return $"{n}th";
        return (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
    }
}

public class SaveResultRequest
{
    public int EnrolmentId { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }
}

// ------------------------------------------------------------------ uploads

public class UploadResponse
{
    public string Url { get; set; } = "";
}

// ----------------------------------------------------- values the API fixes

/// <summary>Mirrors the API's EventStatuses, which the database also constrains.</summary>
public static class EventStatuses
{
    public const string Upcoming = "Upcoming";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Upcoming, Completed, Cancelled };
}

/// <summary>Mirrors the API's EnrolmentStatuses.</summary>
public static class EnrolmentStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Withdrawn = "Withdrawn";
}

public static class Provinces
{
    public static readonly string[] All =
    {
        "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
        "Mpumalanga", "North West", "Northern Cape", "Western Cape"
    };
}
