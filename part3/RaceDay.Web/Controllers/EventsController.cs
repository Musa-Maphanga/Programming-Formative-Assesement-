using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Browsing races and the participant's entry flow.
///
/// The Part 2 API requires a token on every events endpoint, so browsing is
/// for signed-in users. Anonymous visitors are sent to sign in with a return
/// address, which is also what the brief asks for: unauthenticated users must
/// not reach protected pages.
/// </summary>
[RequireRole]
public class EventsController : RaceDayControllerBase
{
    private readonly IRaceDayApiClient _api;

    public EventsController(IRaceDayApiClient api) => _api = api;

    // -------------------------------------------------------------- listing

    [HttpGet]
    public async Task<IActionResult> Index(EventBrowseViewModel model)
    {
        try
        {
            model.Events = await _api.GetEventsAsync(new EventSearch
            {
                Query = model.Query,
                City = model.City,
                Province = model.Province,
                Type = model.Type,
                From = model.From,
                To = model.To,

                // Browsing is for races people can still enter. Cancelled and finished
                // events are reached from an entry or from a results link instead.
                Status = EventStatuses.Upcoming
            });

            model.EventTypes = await SafeGetEventTypes();

            // The soonest race leads the page, but only when the visitor has not
            // filtered - once they are searching, the results are the answer.
            if (!model.HasFilters)
            {
                model.NextUp = model.Events
                    .Where(e => e.EventDate >= DateTime.Now)
                    .OrderBy(e => e.EventDate)
                    .FirstOrDefault();
            }

            return View(model);
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.UserMessage;
            model.EventTypes = await SafeGetEventTypes();
            return View(model);
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "The RaceDay service is not responding, so races cannot be listed right now.";
            return View(model);
        }
    }

    // --------------------------------------------------------------- detail

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var model = new EventDetailViewModel
            {
                Event = await _api.GetEventAsync(id),
                IsSignedIn = HttpContext.Session.IsSignedIn(),
                IsParticipant = HttpContext.Session.IsParticipant()
            };

            if (model.Event.Categories.Count == 0)
            {
                model.Event.Categories = await SafeGetCategories(id);
            }

            // Knowing what the viewer has already entered is what lets the page show
            // "You are entered" instead of offering a duplicate entry.
            if (model.IsParticipant)
            {
                model.MyEnrolments = await SafeGetMyEnrolments();
            }

            if (model.Event.HasTakenPlace)
            {
                model.Results = await SafeGetResults(id);
            }

            return View(model);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            return NotFound();
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Index));
        }
    }

    // ---------------------------------------------------------------- entry

    [HttpPost]
    [RequireRole(Roles.Participant)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int eventId, int categoryId)
    {
        try
        {
            var entry = await _api.EnterEventAsync(new CreateEnrolmentRequest
            {
                EventId = eventId,
                CategoryId = categoryId
            });

            TempData["Success"] = string.IsNullOrEmpty(entry.RaceNumber)
                ? "You are entered."
                : $"You are entered. Your race number is {entry.RaceNumber}.";

            return RedirectToAction("MyEvents", "Participant");
        }
        catch (ApiException ex)
        {
            // 409 is the interesting one here: already entered, the event is full, or
            // entries have closed. The API's own message says which.
            TempData["Error"] = ex.UserMessage;
            return RedirectToAction(nameof(Details), new { id = eventId });
        }
    }

    // -------------------------------------------------------------- results

    /// <summary>Public results for a race that has taken place.</summary>
    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        try
        {
            var model = new EventDetailViewModel
            {
                Event = await _api.GetEventAsync(id),
                Results = await _api.GetEventResultsAsync(id),
                IsSignedIn = HttpContext.Session.IsSignedIn(),
                IsParticipant = HttpContext.Session.IsParticipant()
            };

            return View(model);
        }
        catch (ApiException ex) when (ex.IsNotFound)
        {
            return NotFound();
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Index));
        }
    }

    // -------------------------------------------------------------- helpers

    private async Task<List<EventTypeDto>> SafeGetEventTypes()
    {
        try { return await _api.GetEventTypesAsync(); }
        catch (Exception) { return new List<EventTypeDto>(); }
    }

    private async Task<List<CategoryDto>> SafeGetCategories(int eventId)
    {
        try { return await _api.GetCategoriesAsync(eventId); }
        catch (Exception) { return new List<CategoryDto>(); }
    }

    private async Task<List<EnrolmentDto>> SafeGetMyEnrolments()
    {
        try { return await _api.GetMyEnrolmentsAsync(); }
        catch (Exception) { return new List<EnrolmentDto>(); }
    }

    private async Task<List<ResultDto>> SafeGetResults(int eventId)
    {
        try { return await _api.GetEventResultsAsync(eventId); }
        catch (Exception) { return new List<ResultDto>(); }
    }
}
