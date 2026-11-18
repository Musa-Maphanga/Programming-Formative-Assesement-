using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for capturing or correcting a result.</summary>
public class SaveResultRequest
{
    /// <summary>The entry this result belongs to.</summary>
    /// <example>1</example>
    [Required]
    public int? EnrolmentId { get; set; }

    /// <summary>Finish time as HH:MM:SS.</summary>
    /// <example>01:47:00</example>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position overall in the event, counting from 1.</summary>
    /// <example>118</example>
    [Required, Range(1, int.MaxValue)]
    public int? Position { get; set; }
}

/// <summary>A result as returned by the API.</summary>
public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? RaceNumber { get; set; }

    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }

    /// <summary>Finish time as HH:MM:SS.</summary>
    public TimeSpan FinishTime { get; set; }

    public int Position { get; set; }

    /// <summary>
    /// How many results have been captured for this event, so a client can show a position
    /// as "118th out of 1204 finishers" without a second request.
    /// </summary>
    public int FinisherCount { get; set; }

    public DateTime RecordedAt { get; set; }
}
