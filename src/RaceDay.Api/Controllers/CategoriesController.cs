using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Infrastructure;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>5. Categories – the distances or classes offered inside an event.</summary>
[Route("api")]
[Authorize]
[Tags("5. Categories")]
public class CategoriesController : RaceDayControllerBase
{
    public CategoriesController(RaceDayDbContext db) : base(db) { }

    /// <summary>
    /// Turns a Category row into the response shape. Written as an expression so that
    /// Entity Framework can translate it into SQL, which is what lets EnrolledCount be
    /// counted in the database rather than by loading every enrolment into memory.
    /// </summary>
    public static readonly Expression<Func<Category, CategoryResponse>> Projection = c => new CategoryResponse
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        Name = c.Name,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        EntryFee = c.EntryFee,
        EnrolledCount = c.Enrolments.Count(e => e.Status == EnrolmentStatuses.Confirmed)
    };

    /// <summary>Lists the categories offered by one event.</summary>
    /// <param name="eventId">The EventId.</param>
    /// <response code="200">The event's categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryResponse>>> GetForEvent(int eventId)
    {
        if (!await Db.Events.AnyAsync(e => e.EventId == eventId))
            return ApiError(StatusCodes.Status404NotFound, $"Event {eventId} does not exist.");

        var categories = await Db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderByDescending(c => c.DistanceKm)
            .Select(Projection)
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Shows one category on its own.</summary>
    /// <param name="id">The CategoryId.</param>
    /// <response code="200">The category.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpGet("categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetCategory(int id)
    {
        var category = await Db.Categories.AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(Projection)
            .FirstOrDefaultAsync();

        if (category is null)
            return ApiError(StatusCodes.Status404NotFound, $"Category {id} does not exist.");

        return Ok(category);
    }

    /// <summary>Adds a category to an event.</summary>
    /// <remarks>
    /// Only the organiser who created the event can add categories to it. Category names have to be
    /// unique within an event, which the database also enforces through UQ_Categories_Event_Name.
    /// </remarks>
    /// <param name="eventId">The EventId.</param>
    /// <param name="request">The category details.</param>
    /// <response code="201">The new category.</response>
    /// <response code="400">Invalid fields, or the ages are the wrong way round.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The event does not exist.</response>
    /// <response code="409">The event already has a category with that name.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(int eventId, SaveCategoryRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var ev = await Db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
            return ApiError(StatusCodes.Status404NotFound, $"Event {eventId} does not exist.");
        if (ev.OrganiserId != organiser!.UserId)
            return NotOwner();

        var ageError = CheckAges(request);
        if (ageError is not null) return ageError;

        var name = request.Name.Trim();
        if (await Db.Categories.AnyAsync(c => c.EventId == eventId && c.Name == name))
            return ApiError(StatusCodes.Status409Conflict, $"This event already has a category called \"{name}\".");

        var category = new Category
        {
            EventId = eventId,
            Name = name,
            DistanceKm = request.DistanceKm!.Value,
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            EntryFee = request.EntryFee!.Value
        };

        Db.Categories.Add(category);
        await Db.SaveChangesAsync();

        var created = await Db.Categories.AsNoTracking()
            .Where(c => c.CategoryId == category.CategoryId)
            .Select(Projection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, created);
    }

    /// <summary>Changes a category's details.</summary>
    /// <remarks>
    /// The entry fee is locked once people have entered, because changing it would misrepresent what
    /// those entrants were charged.
    /// </remarks>
    /// <param name="id">The CategoryId.</param>
    /// <param name="request">The new details.</param>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Invalid fields.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">The name is taken, or the fee was changed after people entered.</response>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(int id, SaveCategoryRequest request)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var category = await Db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
            return ApiError(StatusCodes.Status404NotFound, $"Category {id} does not exist.");
        if (category.Event.OrganiserId != organiser!.UserId)
            return NotOwner();

        var ageError = CheckAges(request);
        if (ageError is not null) return ageError;

        var name = request.Name.Trim();
        if (await Db.Categories.AnyAsync(c => c.EventId == category.EventId && c.Name == name && c.CategoryId != id))
            return ApiError(StatusCodes.Status409Conflict, $"This event already has a category called \"{name}\".");

        var entered = await Db.Enrolments.CountAsync(e => e.CategoryId == id && e.Status == EnrolmentStatuses.Confirmed);
        if (entered > 0 && request.EntryFee!.Value != category.EntryFee)
            return ApiError(StatusCodes.Status409Conflict,
                $"{entered} people have already paid to enter this category, so the entry fee can't be changed.");

        category.Name = name;
        category.DistanceKm = request.DistanceKm!.Value;
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.EntryFee = request.EntryFee!.Value;
        await Db.SaveChangesAsync();

        var updated = await Db.Categories.AsNoTracking()
            .Where(c => c.CategoryId == id)
            .Select(Projection)
            .FirstAsync();

        return Ok(updated);
    }

    /// <summary>Removes a category from an event.</summary>
    /// <remarks>A category that people have entered cannot be removed, so that entry history is kept.</remarks>
    /// <param name="id">The CategoryId.</param>
    /// <response code="204">Category removed.</response>
    /// <response code="403">Not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">People have already entered this category.</response>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var (organiser, error) = await RequireUserInRoleAsync(RoleNames.Organiser);
        if (error is not null) return error;

        var category = await Db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
            return ApiError(StatusCodes.Status404NotFound, $"Category {id} does not exist.");
        if (category.Event.OrganiserId != organiser!.UserId)
            return NotOwner();

        if (await Db.Enrolments.AnyAsync(e => e.CategoryId == id))
            return ApiError(StatusCodes.Status409Conflict,
                "People have already entered this category, so it can't be removed.");

        Db.Categories.Remove(category);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // ---------------- helpers ----------------

    private ActionResult? CheckAges(SaveCategoryRequest request)
    {
        if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge > request.MaxAge)
            return FieldError(nameof(request.MinAge), "The minimum age can't be higher than the maximum age.");
        return null;
    }
}
