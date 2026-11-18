using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>6. Enrolments – participants enter events, and organisers see who has entered.</summary>
[Route("api")]
[Authorize]
[Tags("6. Enrolments")]
public class EnrolmentsController : RaceDayControllerBase
{
    public EnrolmentsController(RaceDayDbContext db) : base(db) { }

    /// <summary>
    /// Written as an expression so Entity Framework turns it into one SQL statement instead of
    /// loading the related event, category, participant and result rows separately.
    /// </summary>
    public static readonly Expression<Func<Enrolment, EnrolmentResponse>> Projection = e => new EnrolmentResponse
    {
        EnrolmentId = e.EnrolmentId,

        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant.FirstName + " " + e.Participant.LastName,

        EventId = e.EventId,
        EventName = e.Event.Name,
        EventDate = e.Event.EventDate,
        Location = e.Event.Location,
        City = e.Event.City,
        EventStatus = e.Event.Status,

        CategoryId = e.CategoryId,
        CategoryName = e.Category.Name,
        DistanceKm = e.Category.DistanceKm,
        EntryFee = e.Category.EntryFee,

        RaceNumber = e.RaceNumber,
        Status = e.Status,
        EnrolledAt = e.EnrolledAt,

        Result = e.Result == null
            ? null
            : new ResultResponse
            {
                ResultId = e.Result.ResultId,
                EnrolmentId = e.EnrolmentId,
                ParticipantId = e.ParticipantId,
                ParticipantName = e.Participant.FirstName + " " + e.Participant.LastName,
                RaceNumber = e.RaceNumber,
                EventId = e.EventId,
                EventName = e.Event.Name,
                EventDate = e.Event.EventDate,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                DistanceKm = e.Category.DistanceKm,
                FinishTime = e.Result.FinishTime,
                Position = e.Result.Position,
                FinisherCount = e.Event.Enrolments.Count(other => other.Result != null),
                RecordedAt = e.Result.RecordedAt
            }
    };

    /// <summary>Enters the logged-in participant into one category of an event.</summary>
    /// <remarks>
    /// The entry is confirmed immediately. A participant can hold one entry per event, so to change
    /// category they withdraw and enter again. The API checks that the category belongs to the event,
    /// that the event is still Upcoming, that any age limits on the category are met, and that the
    /// event's participant limit has not been reached.
    /// </remarks>
    /// <response code="201">The new entry.</response>
    /// <response code="400">The category is not part of that event, or an age limit is not met.</response>
    /// <response code="403">Only participants can enter events.</response>
    /// <response code="404">The event or category does not exist.</response>
    /// <response code="409">Already entered, the event is full, or it is no longer open.</response>
    [HttpPost("enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enter(CreateEnrolmentRequest request)
    {
        var (participant, error) = await RequireUserInRoleAsync(RoleNames.Participant);
        if (error is not null) return error;

        var eventId = request.EventId!.Value;
        var categoryId = request.CategoryId!.Value;

        var ev = await Db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {eventId} does not exist.");

        if (ev.Status != EventStatuses.Upcoming)
            return ApiError(StatusCodes.Status409Conflict,
                ev.Status == EventStatuses.Cancelled
                    ? "This event has been cancelled."
                    : "This event has already taken place.");

        if (ev.EventDate <= DateTime.UtcNow)
            return ApiError(StatusCodes.Status409Conflict, "Entries for this event have closed.");

        var category = await Db.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null)
            return ApiError(StatusCodes.Status404NotFound, $"Category {categoryId} does not exist.");

        // The composite foreign key would catch this too, but a clear message is better
        // than a database error surfacing as a 500.
        if (category.EventId != eventId)
            return FieldError(nameof(request.CategoryId), "That category belongs to a different event.");

        var ageError = CheckAge(participant!, category, ev);
        if (ageError is not null) return ageError;

        // UQ_Enrolments_Participant_Event enforces this as well. Checking first turns a
        // constraint violation into a message the participant can act on.
        var existing = await Db.Enrolments
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.ParticipantId == participant!.UserId);

        if (existing is not null && existing.Status == EnrolmentStatuses.Confirmed)
            return ApiError(StatusCodes.Status409Conflict, "You have already entered this event.");

        if (ev.MaxParticipants.HasValue)
        {
            var entered = await Db.Enrolments
                .CountAsync(e => e.EventId == eventId && e.Status == EnrolmentStatuses.Confirmed);
            if (entered >= ev.MaxParticipants.Value)
                return ApiError(StatusCodes.Status409Conflict, "This event is full.");
        }

        int enrolmentId;

        if (existing is not null)
        {
            // Re-entering after withdrawing reuses the row, because the unique key on
            // (ParticipantId, EventId) would refuse a second one.
            existing.CategoryId = categoryId;
            existing.Status = EnrolmentStatuses.Confirmed;
            existing.EnrolledAt = DateTime.UtcNow;
            existing.RaceNumber ??= await NextRaceNumberAsync(eventId);
            await Db.SaveChangesAsync();
            enrolmentId = existing.EnrolmentId;
        }
        else
        {
            var enrolment = new Enrolment
            {
                ParticipantId = participant!.UserId,
                EventId = eventId,
                CategoryId = categoryId,
                RaceNumber = await NextRaceNumberAsync(eventId),
                Status = EnrolmentStatuses.Confirmed,
                EnrolledAt = DateTime.UtcNow
            };
            Db.Enrolments.Add(enrolment);
            await Db.SaveChangesAsync();
            enrolmentId = enrolment.EnrolmentId;
        }

        var created = await Db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == enrolmentId)
            .Select(Projection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetEnrolment), new { id = enrolmentId }, created);
    }

    /// <summary>Shows one entry.</summary>
    /// <remarks>Visible to the participant who made it and to the organiser of the event.</remarks>
    /// <param name="id">The EnrolmentId.</param>
    /// <response code="200">The entry.</response>
    /// <response code="403">Somebody else's entry.</response>
    /// <response code="404">The entry does not exist.</response>
    [HttpGet("enrolments/{id:int}")]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentResponse>> GetEnrolment(int id)
    {
        var enrolment = await Db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == id)
            .Select(Projection)
            .FirstOrDefaultAsync();

        if (enrolment is null)
            return ApiError(StatusCodes.Status404NotFound, $"Entry {id} does not exist.");

        var organiserId = await Db.Events.Where(e => e.EventId == enrolment.EventId)
            .Select(e => e.OrganiserId).FirstAsync();

        if (enrolment.ParticipantId != CurrentUserId && organiserId != CurrentUserId)
            return ApiError(StatusCodes.Status403Forbidden, "You can only see your own entries.");

        return Ok(enrolment);
    }

    /// <summary>Lists every event the logged-in participant has entered, newest first.</summary>
    /// <remarks>This is what fills the participant's "My events" page, including their results.</remarks>
    /// <response code="200">The participant's entries.</response>
    [HttpGet("users/me/enrolments")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetMyEnrolments()
    {
        var (participant, error) = await RequireUserInRoleAsync(RoleNames.Participant);
        if (error is not null) return error;

        var entries = await Db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == participant!.UserId)
            .OrderByDescending(e => e.Event.EventDate)
            .Select(Projection)
            .ToListAsync();

        return Ok(entries);
    }

    /// <summary>Lists everyone entered in one event, for its organiser.</summary>
    /// <param name="id">The EventId.</param>
    /// <param name="status">Optional filter: Confirmed or Withdrawn.</param>
    /// <response code="200">The entry list.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{id:int}/enrolments")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentResponse>>> GetEventEnrolments(int id, [FromQuery] string? status)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var ev = await Db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");
        if (ev.OrganiserId != organiser!.UserId)
            return NotOwner();

        var query = Db.Enrolments.AsNoTracking().Where(e => e.EventId == id);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status.Trim());

        var entries = await query
            .OrderBy(e => e.Category.DistanceKm)
            .ThenBy(e => e.RaceNumber)
            .Select(Projection)
            .ToListAsync();

        return Ok(entries);
    }

    /// <summary>Withdraws the logged-in participant's entry and frees up the place.</summary>
    /// <remarks>
    /// The entry is kept with the status Withdrawn rather than deleted, so the participant's history
    /// stays complete. An entry that already has a result cannot be withdrawn.
    /// </remarks>
    /// <param name="id">The EnrolmentId.</param>
    /// <response code="200">The withdrawn entry.</response>
    /// <response code="403">Somebody else's entry.</response>
    /// <response code="404">The entry does not exist.</response>
    /// <response code="409">Already withdrawn, or a result has been captured.</response>
    [HttpPost("enrolments/{id:int}/withdraw")]
    [Authorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Withdraw(int id)
    {
        var (participant, error) = await RequireUserInRoleAsync(RoleNames.Participant);
        if (error is not null) return error;

        var enrolment = await Db.Enrolments.Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null)
            return ApiError(StatusCodes.Status404NotFound, $"Entry {id} does not exist.");
        if (enrolment.ParticipantId != participant!.UserId)
            return ApiError(StatusCodes.Status403Forbidden, "You can only withdraw your own entries.");

        if (enrolment.Status == EnrolmentStatuses.Withdrawn)
            return ApiError(StatusCodes.Status409Conflict, "This entry has already been withdrawn.");
        if (enrolment.Result is not null)
            return ApiError(StatusCodes.Status409Conflict,
                "A result has already been captured for this entry, so it can't be withdrawn.");

        enrolment.Status = EnrolmentStatuses.Withdrawn;
        await Db.SaveChangesAsync();

        var updated = await Db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == id)
            .Select(Projection)
            .FirstAsync();

        return Ok(updated);
    }

    // ---------------- helpers ----------------

    /// <summary>
    /// Works out the participant's age on race day and checks it against the category's limits.
    /// Age on the day matters, not age today, because a junior category closes on the race date.
    /// </summary>
    private ActionResult? CheckAge(User participant, Category category, Event ev)
    {
        if (category.MinAge is null && category.MaxAge is null) return null;

        if (participant.DateOfBirth is null)
            return ApiError(StatusCodes.Status400BadRequest,
                "This category has an age limit, so add your date of birth to your profile before entering.");

        var raceDay = DateOnly.FromDateTime(ev.EventDate);
        var dob = participant.DateOfBirth.Value;
        var age = raceDay.Year - dob.Year;
        if (dob > raceDay.AddYears(-age)) age--;

        if (category.MinAge.HasValue && age < category.MinAge.Value)
            return ApiError(StatusCodes.Status400BadRequest,
                $"You need to be at least {category.MinAge} on race day to enter this category.");

        if (category.MaxAge.HasValue && age > category.MaxAge.Value)
            return ApiError(StatusCodes.Status400BadRequest,
                $"This category is for entrants up to {category.MaxAge} on race day.");

        return null;
    }

    /// <summary>
    /// Allocates the next race number for an event, counting from 1 and padded to four digits.
    /// Numbers only have to be unique inside their own event, so two events can both issue 0042.
    /// </summary>
    private async Task<string> NextRaceNumberAsync(int eventId)
    {
        var used = await Db.Enrolments
            .Where(e => e.EventId == eventId && e.RaceNumber != null)
            .Select(e => e.RaceNumber!)
            .ToListAsync();

        var highest = used
            .Select(n => int.TryParse(n, out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (highest + 1).ToString("D4");
    }
}
