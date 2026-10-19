using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>4. Events – browse events, and organisers manage their own.</summary>
[Route("api")]
[Authorize]
[Tags("4. Events")]
public class EventsController : RaceDayControllerBase
{
    // Open-Meteo forecasts about 16 days ahead
    private const int ForecastDaysAhead = 15;

    public EventsController(RaceDayDbContext db) : base(db) { }

    /// <summary>Lists events so users can browse them, with optional filters.</summary>
    /// <remarks>
    /// All filters are optional and can be combined, for example
    /// `/api/events?type=Run&amp;province=Gauteng&amp;status=Upcoming`. Results are sorted by event date.
    /// </remarks>
    /// <param name="type">Event type name: Run, Walk or Cycle.</param>
    /// <param name="city">City, for example Durban.</param>
    /// <param name="province">Province, for example Gauteng.</param>
    /// <param name="from">Only events on or after this date.</param>
    /// <param name="to">Only events on or before this date.</param>
    /// <param name="status">Upcoming, Completed or Cancelled.</param>
    /// <response code="200">List of events.</response>
    [HttpGet("events")]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetEvents(
        [FromQuery] string? type, [FromQuery] string? city, [FromQuery] string? province,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status)
    {
        var query = Db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(e => e.EventType.TypeName == type.Trim());
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(e => e.City == city.Trim());
        if (!string.IsNullOrWhiteSpace(province)) query = query.Where(e => e.Province == province.Trim());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status.Trim());
        if (from.HasValue) query = query.Where(e => e.EventDate >= from.Value);
        if (to.HasValue)
        {
            // "to" is inclusive of the whole day
            var end = to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.Date.AddDays(1) : to.Value;
            query = query.Where(e => e.EventDate < end);
        }

        var events = await ProjectEvents(query.OrderBy(e => e.EventDate)).ToListAsync();
        return Ok(events);
    }

    /// <summary>Shows all the details of one event, including its categories.</summary>
    /// <param name="id">The EventId.</param>
    /// <response code="200">The event and its categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{id:int}")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> GetEvent(int id)
    {
        var ev = await ProjectEvents(Db.Events.AsNoTracking().Where(e => e.EventId == id)).FirstOrDefaultAsync();
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");

        var detail = ToDetail(ev);
        detail.Categories = await Db.Categories.AsNoTracking()
            .Where(c => c.EventId == id)
            .OrderBy(c => c.CategoryId)
            .Select(c => CategoriesController.ToResponse(c))
            .ToListAsync();

        return Ok(detail);
    }

    /// <summary>Lists only the events that the logged-in organiser created, for their dashboard.</summary>
    /// <response code="200">List of the organiser's events.</response>
    [HttpGet("users/me/events")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventResponse>>> GetMyEvents()
    {
        var (user, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var events = await ProjectEvents(Db.Events.AsNoTracking()
                .Where(e => e.OrganiserId == user!.UserId)
                .OrderBy(e => e.EventDate))
            .ToListAsync();
        return Ok(events);
    }

    /// <summary>Creates a new event, and the logged-in organiser becomes its owner.</summary>
    /// <remarks>
    /// The OrganiserId is taken from the token, never from the request body, and the API checks that this
    /// user's role in the database really is Organiser before saving. The database foreign key alone can't do that.
    /// New events always start with the status Upcoming.
    /// </remarks>
    /// <response code="201">The new event.</response>
    /// <response code="400">Invalid fields or the date is in the past.</response>
    /// <response code="404">The event type does not exist.</response>
    [HttpPost("events")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> CreateEvent(CreateEventRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        if (request.EventDate!.Value <= DateTime.UtcNow)
            return FieldError(nameof(request.EventDate), "The event date must be in the future.");

        var coordError = CheckCoordinates(request);
        if (coordError is not null) return coordError;

        if (!await Db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
            return ApiError(StatusCodes.Status404NotFound, $"Event type {request.EventTypeId} does not exist.");

        var ev = new Event
        {
            OrganiserId = organiser!.UserId,
            Status = EventStatuses.Upcoming,
            CreatedAt = DateTime.UtcNow
        };
        Apply(ev, request);

        Db.Events.Add(ev);
        await Db.SaveChangesAsync();

        var created = await ProjectEvents(Db.Events.AsNoTracking().Where(e => e.EventId == ev.EventId)).FirstAsync();
        return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ToDetail(created));
    }

    /// <summary>Updates an event's details or changes its status (for example to Cancelled).</summary>
    /// <remarks>Only the organiser who created the event can change it.</remarks>
    /// <param name="id">The EventId.</param>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Invalid fields.</response>
    /// <response code="403">Not the owner.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPut("events/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailResponse>> UpdateEvent(int id, UpdateEventRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var ev = await Db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");
        if (ev.OrganiserId != organiser!.UserId)
            return NotOwner();

        var status = EventStatuses.All.FirstOrDefault(s => string.Equals(s, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status is null)
            return FieldError(nameof(request.Status), "Status must be Upcoming, Completed or Cancelled.");

        var coordError = CheckCoordinates(request);
        if (coordError is not null) return coordError;

        if (!await Db.EventTypes.AnyAsync(t => t.EventTypeId == request.EventTypeId))
            return FieldError(nameof(request.EventTypeId), $"Event type {request.EventTypeId} does not exist.");

        var entered = await Db.Enrolments.CountAsync(e => e.EventId == id && e.Status == EnrolmentStatuses.Confirmed);
        if (request.MaxParticipants.HasValue && request.MaxParticipants.Value < entered)
            return FieldError(nameof(request.MaxParticipants), $"{entered} people have already entered, so the limit can't be lower than that.");

        Apply(ev, request);
        ev.Status = status;
        await Db.SaveChangesAsync();

        var updated = await ProjectEvents(Db.Events.AsNoTracking().Where(e => e.EventId == id)).FirstAsync();
        return Ok(ToDetail(updated));
    }

    /// <summary>Deletes an event, but only if nobody has entered it yet.</summary>
    /// <remarks>
    /// If people have entered, the organiser should set the status to Cancelled instead, so that
    /// participants' entry history is kept. The event's categories are removed with it.
    /// </remarks>
    /// <param name="id">The EventId.</param>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">Not the owner.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The event already has enrolments.</response>
    [HttpDelete("events/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var ev = await Db.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");
        if (ev.OrganiserId != organiser!.UserId)
            return NotOwner();

        if (await Db.Enrolments.AnyAsync(e => e.EventId == id))
            return ApiError(StatusCodes.Status409Conflict, "People have already entered this event. Set its status to Cancelled instead.");

        Db.Categories.RemoveRange(ev.Categories);
        Db.Events.Remove(ev);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Gets the weather forecast for the event's location on race day from an external weather API.</summary>
    /// <remarks>
    /// Uses the event's latitude and longitude with the Open-Meteo forecast service. Forecasts are only
    /// available about two weeks ahead, so for events further away (or already over, or without coordinates)
    /// the response has `forecastAvailable: false` and a message explaining why.
    /// </remarks>
    /// <param name="id">The EventId.</param>
    /// <param name="weather">Injected weather service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Weather forecast.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="502">The weather service is down.</response>
    [HttpGet("events/{id:int}/weather")]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<WeatherResponse>> GetWeather(int id, [FromServices] IWeatherService weather, CancellationToken ct)
    {
        var ev = await Db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {id} does not exist.");

        var raceDay = DateOnly.FromDateTime(ev.EventDate);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new WeatherResponse
        {
            EventId = ev.EventId,
            EventName = ev.Name,
            Date = raceDay,
            Location = $"{ev.Location}, {ev.City}"
        };

        if (ev.Latitude is null || ev.Longitude is null)
        {
            response.Message = "This event has no coordinates, so no forecast can be looked up.";
            return Ok(response);
        }
        if (raceDay < today)
        {
            response.Message = "This event has already taken place.";
            return Ok(response);
        }
        if (raceDay > today.AddDays(ForecastDaysAhead))
        {
            response.Message = $"A forecast will be available from {raceDay.AddDays(-ForecastDaysAhead):yyyy-MM-dd}.";
            return Ok(response);
        }

        try
        {
            var f = await weather.GetDailyForecastAsync(ev.Latitude.Value, ev.Longitude.Value, raceDay, ct);
            response.ForecastAvailable = true;
            response.Summary = f.Summary;
            response.MinTempC = f.MinTempC;
            response.MaxTempC = f.MaxTempC;
            response.RainChancePercent = f.RainChancePercent;
            response.MaxWindKmh = f.MaxWindKmh;
            return Ok(response);
        }
        catch (WeatherServiceUnavailableException)
        {
            return ApiError(StatusCodes.Status502BadGateway, "The weather service is not available right now. Please try again later.");
        }
    }

    // ---------------- helpers ----------------

    private ActionResult? CheckCoordinates(CreateEventRequest request)
    {
        if (request.Latitude.HasValue != request.Longitude.HasValue)
            return FieldError(nameof(request.Latitude), "Give both latitude and longitude, or neither.");
        return null;
    }

    private static void Apply(Event ev, CreateEventRequest r)
    {
        ev.Name = r.Name.Trim();
        ev.Description = r.Description;
        ev.EventDate = r.EventDate!.Value;
        ev.Location = r.Location.Trim();
        ev.City = r.City.Trim();
        ev.Province = r.Province.Trim();
        ev.Latitude = r.Latitude;
        ev.Longitude = r.Longitude;
        ev.DistanceKm = r.DistanceKm!.Value;
        ev.EventTypeId = r.EventTypeId!.Value;
        ev.MaxParticipants = r.MaxParticipants;
    }

    private static IQueryable<EventResponse> ProjectEvents(IQueryable<Event> query) =>
        query.Select(e => new EventResponse
        {
            EventId = e.EventId,
            Name = e.Name,
            Description = e.Description,
            EventDate = e.EventDate,
            Location = e.Location,
            City = e.City,
            Province = e.Province,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            DistanceKm = e.DistanceKm,
            EventTypeId = e.EventTypeId,
            EventType = e.EventType.TypeName,
            MaxParticipants = e.MaxParticipants,
            EnrolledCount = e.Enrolments.Count(en => en.Status == EnrolmentStatuses.Confirmed),
            Status = e.Status,
            OrganiserId = e.OrganiserId,
            OrganiserName = e.Organiser.FirstName + " " + e.Organiser.LastName,
            CreatedAt = e.CreatedAt
        });

    private static EventDetailResponse ToDetail(EventResponse e) => new()
    {
        EventId = e.EventId,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        City = e.City,
        Province = e.Province,
        Latitude = e.Latitude,
        Longitude = e.Longitude,
        DistanceKm = e.DistanceKm,
        EventTypeId = e.EventTypeId,
        EventType = e.EventType,
        MaxParticipants = e.MaxParticipants,
        EnrolledCount = e.EnrolledCount,
        Status = e.Status,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.OrganiserName,
        CreatedAt = e.CreatedAt
    };
}
