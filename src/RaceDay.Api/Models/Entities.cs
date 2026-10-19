namespace RaceDay.Api.Models;

// One class per table in the Part 1 ERD. Column sizes, defaults, keys and
// constraints are configured in Data/RaceDayDbContext.cs so they match
// docs/RaceDay_Schema.sql.

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;

    /// <summary>Events this user organises (only filled for Organisers).</summary>
    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();

    /// <summary>Events this user has entered (only filled for Participants).</summary>
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();

    /// <summary>Results this user captured. One organiser records many results;
    /// each result is recorded by exactly one user (Results.RecordedBy).</summary>
    public ICollection<Result> RecordedResults { get; set; } = new List<Result>();
}

public class EventType
{
    public int EventTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}

public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public int EventTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
    public string Status { get; set; } = EventStatuses.Upcoming;
    public DateTime CreatedAt { get; set; }

    public User Organiser { get; set; } = null!;
    public EventType EventType { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public string? RaceNumber { get; set; }
    public string Status { get; set; } = EnrolmentStatuses.Confirmed;
    public DateTime EnrolledAt { get; set; }

    public User Participant { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}

public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan FinishTime { get; set; }
    public int Position { get; set; }

    /// <summary>FK to Users.UserId – the organiser who captured this result.</summary>
    public int RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; }

    public Enrolment Enrolment { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
}
