using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Models.ViewModels;

/*
 * View models carry exactly what a page needs and nothing else. Validation
 * attributes live here so that an obviously bad form is caught and explained
 * before a request is sent. The API validates again regardless; this layer
 * exists to give the user a fast, readable answer, not to be the only line of
 * defence.
 */

// ------------------------------------------------------------------ account

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter the email address you registered with.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Enter your surname.")]
    [StringLength(50)]
    [Display(Name = "Surname")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = "";

    /// <summary>
    /// The API fixes a user's role at registration, so the choice has to be made here.
    /// It accepts only Organiser or Participant.
    /// </summary>
    [Required(ErrorMessage = "Choose whether you are entering races or running them.")]
    public string Role { get; set; } = Roles.Participant;

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Passwords need at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Phone(ErrorMessage = "That does not look like a phone number.")]
    [Display(Name = "Mobile number")]
    public string? PhoneNumber { get; set; }

    /// <summary>Needed for categories that carry an age limit.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    [Display(Name = "Town or city")]
    public string? City { get; set; }

    public static readonly string[] Genders = { "Male", "Female", "Other" };
}

public class ProfileViewModel
{
    public UserProfile Profile { get; set; } = new();

    [Required(ErrorMessage = "Enter your first name.")]
    [StringLength(50)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Enter your surname.")]
    [StringLength(50)]
    [Display(Name = "Surname")]
    public string LastName { get; set; } = "";

    [Phone(ErrorMessage = "That does not look like a phone number.")]
    [Display(Name = "Mobile number")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    [Display(Name = "Town or city")]
    public string? City { get; set; }

    [Display(Name = "Profile picture")]
    public IFormFile? ProfilePicture { get; set; }
}

// ----------------------------------------------------------------- browsing

public class EventBrowseViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public List<EventTypeDto> EventTypes { get; set; } = new();
    public EventDto? NextUp { get; set; }

    [Display(Name = "Search")]
    public string? Query { get; set; }

    [Display(Name = "Town or city")]
    public string? City { get; set; }

    public string? Province { get; set; }

    /// <summary>The API filters by type name, not by id.</summary>
    [Display(Name = "Discipline")]
    public string? Type { get; set; }

    [DataType(DataType.Date), Display(Name = "From")]
    public DateTime? From { get; set; }

    [DataType(DataType.Date), Display(Name = "To")]
    public DateTime? To { get; set; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrWhiteSpace(City)
        || !string.IsNullOrWhiteSpace(Province) || !string.IsNullOrWhiteSpace(Type)
        || From.HasValue || To.HasValue;
}

public class EventDetailViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> MyEnrolments { get; set; } = new();
    public List<ResultDto> Results { get; set; } = new();
    public bool IsSignedIn { get; set; }
    public bool IsParticipant { get; set; }

    /// <summary>
    /// A participant holds one entry per event, so the page shows which category
    /// they are in rather than offering every category again.
    /// </summary>
    public EnrolmentDto? MyEntry =>
        MyEnrolments.FirstOrDefault(e => e.EventId == Event.EventId && e.IsConfirmed);
}

// ---------------------------------------------------------------- organiser

public class OrganiserDashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();

    public int TotalEvents => Events.Count;
    public int UpcomingCount => Events.Count(e => e.Status == EventStatuses.Upcoming);
    public int TotalEnrolments => Events.Sum(e => e.EnrolledCount);

    public List<EventDto> Upcoming => Events
        .Where(e => e.Status == EventStatuses.Upcoming && e.EventDate >= DateTime.Now)
        .OrderBy(e => e.EventDate)
        .ToList();

    public EventDto? NextEvent => Upcoming.FirstOrDefault();

    /// <summary>Events that have happened but still have entries without a result.</summary>
    public List<EventDto> AwaitingResults => Events
        .Where(e => e.HasTakenPlace && e.Status != EventStatuses.Cancelled && e.EnrolledCount > 0)
        .OrderByDescending(e => e.EventDate)
        .ToList();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Give the event a name.")]
    [StringLength(150)]
    [Display(Name = "Event name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Choose a discipline.")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a discipline.")]
    [Display(Name = "Discipline")]
    public int EventTypeId { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>The API stores the date and the start time together.</summary>
    [Required(ErrorMessage = "Set the date and start time.")]
    [Display(Name = "Date and start time")]
    public DateTime EventDate { get; set; } = DateTime.Today.AddMonths(2).AddHours(6);

    [Required(ErrorMessage = "Name the start venue.")]
    [StringLength(200)]
    [Display(Name = "Venue")]
    public string Location { get; set; } = "";

    [Required(ErrorMessage = "Enter the town or city.")]
    [StringLength(100)]
    [Display(Name = "Town or city")]
    public string City { get; set; } = "";

    [Required(ErrorMessage = "Choose a province.")]
    public string Province { get; set; } = "";

    [Required(ErrorMessage = "Enter the main distance.")]
    [Range(0.01, 9999.99, ErrorMessage = "The distance must be greater than zero.")]
    [Display(Name = "Main distance (km)")]
    public decimal DistanceKm { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "The entry limit must be at least 1.")]
    [Display(Name = "Entry limit")]
    public int? MaxParticipants { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    public string Status { get; set; } = EventStatuses.Upcoming;

    [Display(Name = "Event banner")]
    public IFormFile? Banner { get; set; }

    public string? ExistingBannerUrl { get; set; }

    public List<EventTypeDto> EventTypes { get; set; } = new();

    public IEnumerable<SelectListItem> ProvinceOptions =>
        Provinces.All.Select(p => new SelectListItem(p, p, p == Province));

    public bool IsEdit => EventId.HasValue;

    /// <summary>
    /// The API refuses a past date on create, so it is worth catching here, where the
    /// message can sit against the field instead of arriving as a failed request.
    /// </summary>
    public IEnumerable<ValidationResult> ValidateDate()
    {
        if (!IsEdit && EventDate <= DateTime.Now)
        {
            yield return new ValidationResult(
                "The event date has to be in the future.", new[] { nameof(EventDate) });
        }

        if (Latitude.HasValue != Longitude.HasValue)
        {
            yield return new ValidationResult(
                "Give both latitude and longitude, or neither.", new[] { nameof(Latitude) });
        }
    }
}

public class CategoryFormViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Name the category, for example \"21.1km Half Marathon\".")]
    [StringLength(50, ErrorMessage = "Category names are limited to 50 characters.")]
    [Display(Name = "Category name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Enter the distance.")]
    [Range(0.01, 9999.99, ErrorMessage = "The distance must be greater than zero.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; }

    [Required(ErrorMessage = "Enter an entry fee, or zero for a free race.")]
    [Range(0, 999999.99, ErrorMessage = "The entry fee cannot be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }

    [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    public List<CategoryDto> ExistingCategories { get; set; } = new();

    public bool IsEdit => CategoryId.HasValue;

    public IEnumerable<ValidationResult> ValidateAges()
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
        {
            yield return new ValidationResult(
                "The minimum age can't be higher than the maximum age.", new[] { nameof(MinAge) });
        }
    }
}

public class EnrolmentListViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();

    public int Confirmed => Enrolments.Count(e => e.IsConfirmed);
    public int Withdrawn => Enrolments.Count(e => !e.IsConfirmed);

    public IEnumerable<IGrouping<string, EnrolmentDto>> ByCategory =>
        Enrolments.Where(e => e.IsConfirmed).GroupBy(e => e.CategoryName);
}

public class ResultEntryViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();

    [Display(Name = "Participant")]
    public int EnrolmentId { get; set; }

    [Required(ErrorMessage = "Enter the finish time.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$",
        ErrorMessage = "Enter the finish time as HH:MM:SS, for example 01:47:00.")]
    [Display(Name = "Finish time")]
    public string? FinishTime { get; set; }

    /// <summary>Nullable so the field starts empty rather than showing a stray 0.</summary>
    [Required(ErrorMessage = "Enter the finishing position.")]
    [Range(1, int.MaxValue, ErrorMessage = "Position must be 1 or higher.")]
    [Display(Name = "Finishing position")]
    public int? Position { get; set; }

    /// <summary>Entries still waiting for a result, which is what the form offers.</summary>
    public List<EnrolmentDto> Outstanding =>
        Enrolments.Where(e => e.IsConfirmed && e.Result is null).ToList();

    public List<EnrolmentDto> Captured =>
        Enrolments.Where(e => e.Result is not null)
            .OrderBy(e => e.Result!.Position)
            .ToList();

    public TimeSpan? ParsedFinishTime =>
        TimeSpan.TryParse(FinishTime, out var parsed) ? parsed : null;
}

// --------------------------------------------------------------- participant

public class MyEventsViewModel
{
    public List<EnrolmentDto> Enrolments { get; set; } = new();

    public List<EnrolmentDto> Upcoming => Enrolments
        .Where(e => e.IsConfirmed && e.IsUpcoming)
        .OrderBy(e => e.EventDate)
        .ToList();

    public List<EnrolmentDto> Past => Enrolments
        .Where(e => !e.IsConfirmed || !e.IsUpcoming)
        .OrderByDescending(e => e.EventDate)
        .ToList();
}

public class MyResultsViewModel
{
    public List<ResultDto> Results { get; set; } = new();

    public int RacesCompleted => Results.Count;

    public decimal TotalDistance => Results.Sum(r => r.DistanceKm);

    public ResultDto? Fastest => Results
        .Where(r => r.DistanceKm > 0 && r.FinishTime > TimeSpan.Zero)
        .OrderBy(r => r.FinishTime.TotalSeconds / (double)r.DistanceKm)
        .FirstOrDefault();
}
