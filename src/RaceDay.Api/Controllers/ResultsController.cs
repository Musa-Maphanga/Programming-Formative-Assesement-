using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>7. Results – organisers capture finish times, participants read their race history.</summary>
[Route("api")]
[Authorize]
[Tags("7. Results")]
public class ResultsController : RaceDayControllerBase
{
    public ResultsController(RaceDayDbContext db) : base(db) { }

    /// <summary>Translated into SQL by Entity Framework, so one query fills the whole response.</summary>
    public static readonly Expression<Func<Result, ResultResponse>> Projection = r => new ResultResponse
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,

        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.FirstName + " " + r.Enrolment.Participant.LastName,
        RaceNumber = r.Enrolment.RaceNumber,

        EventId = r.Enrolment.EventId,
        EventName = r.Enrolment.Event.Name,
        EventDate = r.Enrolment.Event.EventDate,

        CategoryId = r.Enrolment.CategoryId,
        CategoryName = r.Enrolment.Category.Name,
        DistanceKm = r.Enrolment.Category.DistanceKm,

        FinishTime = r.FinishTime,
        Position = r.Position,
        FinisherCount = r.Enrolment.Event.Enrolments.Count(e => e.Result != null),
        RecordedAt = r.RecordedAt
    };

    /// <summary>Captures the result for one entry.</summary>
    /// <remarks>
    /// Only the organiser of the event can capture its results, and only after the event date has
    /// passed. Each entry can have one result; correcting an existing one is a PUT.
    /// </remarks>
    /// <response code="201">The captured result.</response>
    /// <response code="400">The position is already taken by another finisher.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The entry does not exist.</response>
    /// <response code="409">A result already exists, the entry was withdrawn, or the event has not happened yet.</response>
    [HttpPost("results")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(SaveResultRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var enrolmentId = request.EnrolmentId!.Value;

        var enrolment = await Db.Enrolments
            .Include(e => e.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null)
            return ApiError(StatusCodes.Status404NotFound, $"Entry {enrolmentId} does not exist.");
        if (enrolment.Event.OrganiserId != organiser!.UserId)
            return NotOwner("events' results");

        if (enrolment.Status == EnrolmentStatuses.Withdrawn)
            return ApiError(StatusCodes.Status409Conflict,
                "This entry was withdrawn, so it cannot be given a result.");

        if (enrolment.Result is not null)
            return ApiError(StatusCodes.Status409Conflict,
                $"This entry already has a result. Use PUT /api/results/{enrolment.Result.ResultId} to correct it.");

        if (enrolment.Event.EventDate > DateTime.UtcNow)
            return ApiError(StatusCodes.Status409Conflict,
                "This event has not taken place yet, so there are no results to capture.");

        var timeError = CheckFinishTime(request);
        if (timeError is not null) return timeError;

        var positionError = await CheckPositionAsync(enrolment.EventId, request.Position!.Value, null);
        if (positionError is not null) return positionError;

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime!.Value,
            Position = request.Position!.Value,
            RecordedBy = organiser.UserId,
            RecordedAt = DateTime.UtcNow
        };

        Db.Results.Add(result);
        await Db.SaveChangesAsync();

        var created = await Db.Results.AsNoTracking()
            .Where(r => r.ResultId == result.ResultId)
            .Select(Projection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetResult), new { id = result.ResultId }, created);
    }

    /// <summary>Corrects a result that has already been captured.</summary>
    /// <remarks>Used after a timing dispute. The entry a result belongs to cannot be changed.</remarks>
    /// <param name="id">The ResultId.</param>
    /// <param name="request">The corrected time and position.</param>
    /// <response code="200">The corrected result.</response>
    /// <response code="400">The position is already taken by another finisher.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The result does not exist.</response>
    [HttpPut("results/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int id, SaveResultRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var result = await Db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        if (result is null)
            return ApiError(StatusCodes.Status404NotFound, $"Result {id} does not exist.");
        if (result.Enrolment.Event.OrganiserId != organiser!.UserId)
            return NotOwner("events' results");

        var timeError = CheckFinishTime(request);
        if (timeError is not null) return timeError;

        var positionError = await CheckPositionAsync(result.Enrolment.EventId, request.Position!.Value, id);
        if (positionError is not null) return positionError;

        result.FinishTime = request.FinishTime!.Value;
        result.Position = request.Position!.Value;
        result.RecordedBy = organiser.UserId;
        result.RecordedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();

        var updated = await Db.Results.AsNoTracking()
            .Where(r => r.ResultId == id)
            .Select(Projection)
            .FirstAsync();

        return Ok(updated);
    }

    /// <summary>Shows one result.</summary>
    /// <param name="id">The ResultId.</param>
    /// <response code="200">The result.</response>
    /// <response code="404">The result does not exist.</response>
    [HttpGet("results/{id:int}")]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultResponse>> GetResult(int id)
    {
        var result = await Db.Results.AsNoTracking()
            .Where(r => r.ResultId == id)
            .Select(Projection)
            .FirstOrDefaultAsync();

        if (result is null)
            return ApiError(StatusCodes.Status404NotFound, $"Result {id} does not exist.");

        return Ok(result);
    }

    /// <summary>Lists the results for one event, fastest first.</summary>
    /// <param name="id">The EventId.</param>
    /// <response code="200">The event's results.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{id:int}/results")]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultResponse>>> GetEventResults(int id)
    {
        if (!await Db.Events.AnyAsync(e => e.EventId == id))
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");

        var results = await Db.Results.AsNoTracking()
            .Where(r => r.Enrolment.EventId == id)
            .OrderBy(r => r.Position)
            .Select(Projection)
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>The logged-in participant's own race history, newest race first.</summary>
    /// <remarks>This is what fills the participant's "My results" page.</remarks>
    /// <response code="200">The participant's results.</response>
    [HttpGet("users/me/results")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ResultResponse>>> GetMyResults()
    {
        var (participant, error) = await RequireUserInRoleAsync(RoleNames.Participant);
        if (error is not null) return error;

        var results = await Db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == participant!.UserId)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .Select(Projection)
            .ToListAsync();

        return Ok(results);
    }

    // ---------------- helpers ----------------

    private ActionResult? CheckFinishTime(SaveResultRequest request)
    {
        var time = request.FinishTime!.Value;

        if (time <= TimeSpan.Zero)
            return FieldError(nameof(request.FinishTime), "The finish time must be greater than zero.");

        // The column is time(0), which cannot hold 24 hours or more.
        if (time >= TimeSpan.FromDays(1))
            return FieldError(nameof(request.FinishTime), "The finish time must be less than 24 hours.");

        return null;
    }

    /// <summary>
    /// Two finishers in the same event cannot hold the same position. The database has no
    /// constraint for this because positions are only unique within an event, so it is checked here.
    /// </summary>
    private async Task<ActionResult?> CheckPositionAsync(int eventId, int position, int? ignoreResultId)
    {
        var taken = await Db.Results.AnyAsync(r =>
            r.Enrolment.EventId == eventId
            && r.Position == position
            && (ignoreResultId == null || r.ResultId != ignoreResultId));

        if (taken)
            return ApiError(StatusCodes.Status400BadRequest,
                $"Position {position} has already been given to another finisher in this event.");

        return null;
    }
}
