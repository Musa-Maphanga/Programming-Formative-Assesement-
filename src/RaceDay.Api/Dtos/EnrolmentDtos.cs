using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for entering an event.</summary>
/// <remarks>
/// The participant is taken from the token, never from the body, so one user can never
/// enter an event on somebody else's behalf.
/// </remarks>
public class CreateEnrolmentRequest
{
    /// <example>1</example>
    [Required]
    public int? EventId { get; set; }

    /// <summary>Must be a category belonging to that event.</summary>
    /// <example>2</example>
    [Required]
    public int? CategoryId { get; set; }
}

/// <summary>An entry as returned by the API.</summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;

    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string EventStatus { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public decimal EntryFee { get; set; }

    /// <summary>Allocated when the entry is accepted. Unique within the event.</summary>
    public string? RaceNumber { get; set; }

    /// <summary>Confirmed or Withdrawn.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime EnrolledAt { get; set; }

    /// <summary>The participant's result, once the organiser has captured it.</summary>
    public ResultResponse? Result { get; set; }
}
