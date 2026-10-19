using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;

namespace RaceDay.Api.Controllers;

/// <summary>3. Event Types – lookup list for forms.</summary>
[Route("api/event-types")]
[AllowAnonymous]
[Tags("3. Event Types")]
public class EventTypesController : RaceDayControllerBase
{
    public EventTypesController(RaceDayDbContext db) : base(db) { }

    /// <summary>Gets the list of event types (Run, Walk, Cycle) so the front end can show them in a dropdown.</summary>
    /// <response code="200">List of event types.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventTypeResponse>>> GetEventTypes()
    {
        var types = await Db.EventTypes
            .OrderBy(t => t.EventTypeId)
            .Select(t => new EventTypeResponse { EventTypeId = t.EventTypeId, TypeName = t.TypeName })
            .ToListAsync();
        return Ok(types);
    }
}
