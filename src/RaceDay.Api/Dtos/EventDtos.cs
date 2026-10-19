using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for creating an event.</summary>
public class CreateEventRequest
{
    /// <example>Soweto Sunrise 10km</example>
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <example>An early morning road race through Soweto.</example>
    [StringLength(2000)]
    public string? Description { get; set; }

    /// <summary>Date and start time of the event. Must be in the future.</summary>
    /// <example>2026-12-06T06:00:00</example>
    [Required]
    public DateTime? EventDate { get; set; }

    /// <example>Orlando Stadium</example>
    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    /// <example>Johannesburg</example>
    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    /// <example>Gauteng</example>
    [Required, StringLength(50)]
    public string Province { get; set; } = string.Empty;

    /// <summary>Used for the race-day weather lookup.</summary>
    /// <example>-26.2349</example>
    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    /// <example>27.9082</example>
    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    /// <example>10</example>
    [Required, Range(0.01, 9999.99)]
    public decimal? DistanceKm { get; set; }

    /// <summary>1 = Run, 2 = Walk, 3 = Cycle (see GET /api/event-types).</summary>
    /// <example>1</example>
    [Required]
    public int? EventTypeId { get; set; }

    /// <summary>Leave empty for no limit.</summary>
    /// <example>500</example>
    [Range(1, int.MaxValue)]
    public int? MaxParticipants { get; set; }
}

/// <summary>Body for updating an event. Same fields as create, plus the status.</summary>
public class UpdateEventRequest : CreateEventRequest
{
    /// <summary>Upcoming, Completed or Cancelled.</summary>
    /// <example>Upcoming</example>
    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>An event as returned by the API.</summary>
public class EventResponse
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal DistanceKm { get; set; }
    public int EventTypeId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int? MaxParticipants { get; set; }
    public int EnrolledCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>An event with its categories (GET /api/events/{id}).</summary>
public class EventDetailResponse : EventResponse
{
    public List<CategoryResponse> Categories { get; set; } = new();
}

/// <summary>A lookup row from EventTypes.</summary>
public class EventTypeResponse
{
    public int EventTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
}

/// <summary>Race-day forecast for an event's location.</summary>
public class WeatherResponse
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Location { get; set; } = string.Empty;

    /// <summary>False when the event is too far away for a forecast, already over, or has no coordinates.</summary>
    public bool ForecastAvailable { get; set; }
    public string? Message { get; set; }
    public string? Summary { get; set; }
    public decimal? MinTempC { get; set; }
    public decimal? MaxTempC { get; set; }
    public int? RainChancePercent { get; set; }
    public decimal? MaxWindKmh { get; set; }
}
