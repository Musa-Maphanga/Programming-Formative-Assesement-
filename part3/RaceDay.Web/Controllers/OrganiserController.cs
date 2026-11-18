using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Everything an organiser does: the dashboard, event management, categories,
/// the entry list and results capture. The role filter sits on the controller,
/// so every action inside it is protected without being marked individually.
/// </summary>
[RequireRole(Roles.Organiser)]
public class OrganiserController : RaceDayControllerBase
{
    private readonly IRaceDayApiClient _api;

    public OrganiserController(IRaceDayApiClient api) => _api = api;

    // ----------------------------------------------------------- dashboard

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(new OrganiserDashboardViewModel { Events = await _api.GetMyEventsAsync() });
        }
        catch (ApiException ex)
        {
            if (ex.IsUnauthorised) return HandleApiFailure(ex, "Login", "Account");
            TempData["Error"] = ex.UserMessage;
            return View(new OrganiserDashboardViewModel());
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "The RaceDay service is not responding, so your events cannot be loaded.";
            return View(new OrganiserDashboardViewModel());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Events()
    {
        try
        {
            return View(await _api.GetMyEventsAsync());
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Index));
        }
    }

    // ------------------------------------------------------- create / edit

    [HttpGet]
    public async Task<IActionResult> Create()
        => View("EventForm", new EventFormViewModel { EventTypes = await SafeGetEventTypes() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        ApplyExtraRules(model);

        if (!ValidateImage(model.Banner, nameof(model.Banner)) || !ModelState.IsValid)
        {
            model.EventTypes = await SafeGetEventTypes();
            return View("EventForm", model);
        }

        try
        {
            var created = await _api.CreateEventAsync(ToCreateRequest(model));
            await UploadBannerIfSupplied(created.EventId, model.Banner);

            TempData["Success"] =
                $"\"{created.Name}\" has been created. Add the distances people can enter.";
            return RedirectToAction(nameof(Categories), new { id = created.EventId });
        }
        catch (ApiException ex)
        {
            AddApiErrorsToModelState(ex);
            model.EventTypes = await SafeGetEventTypes();
            return View("EventForm", model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var model = ToViewModel(await _api.GetEventAsync(id));
            model.EventTypes = await SafeGetEventTypes();
            return View("EventForm", model);
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Events));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ApplyExtraRules(model);

        if (!ValidateImage(model.Banner, nameof(model.Banner)) || !ModelState.IsValid)
        {
            model.EventTypes = await SafeGetEventTypes();
            return View("EventForm", model);
        }

        try
        {
            await _api.UpdateEventAsync(id, ToUpdateRequest(model));
            await UploadBannerIfSupplied(id, model.Banner);

            TempData["Success"] = "Your changes have been saved.";
            return RedirectToAction(nameof(Events));
        }
        catch (ApiException ex)
        {
            AddApiErrorsToModelState(ex);
            model.EventTypes = await SafeGetEventTypes();
            return View("EventForm", model);
        }
    }

    /// <summary>
    /// Changes an event's status. The API carries status on the whole event, so the
    /// client reads the event first and sends it back with the new status.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, string status)
    {
        try
        {
            await _api.SetEventStatusAsync(id, status);
            TempData["Success"] = status switch
            {
                EventStatuses.Cancelled =>
                    "The event has been cancelled. Entrants keep it in their history.",
                EventStatuses.Completed =>
                    "The event is marked as completed. You can now capture results.",
                _ => "The event status has been updated."
            };
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Events));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _api.DeleteEventAsync(id);
            TempData["Success"] = "The event has been deleted.";
        }
        catch (ApiException ex)
        {
            // The API refuses to delete an event that already has entries, and tells
            // the organiser to cancel it instead. That refusal is worth showing in full.
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Events));
    }

    // ------------------------------------------------------------ categories

    [HttpGet]
    public async Task<IActionResult> Categories(int id)
    {
        try
        {
            var ev = await _api.GetEventAsync(id);
            return View(new CategoryFormViewModel
            {
                EventId = id,
                EventName = ev.Name,
                ExistingCategories = ev.Categories.Count > 0
                    ? ev.Categories
                    : await _api.GetCategoriesAsync(id)
            });
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Events));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Categories(CategoryFormViewModel model)
    {
        foreach (var failure in model.ValidateAges())
        {
            ModelState.AddModelError(failure.MemberNames.FirstOrDefault() ?? string.Empty,
                failure.ErrorMessage ?? "Those ages are not valid.");
        }

        if (!ModelState.IsValid)
        {
            model.ExistingCategories = await SafeGetCategories(model.EventId);
            return View(model);
        }

        var request = new SaveCategoryRequest
        {
            Name = model.Name,
            DistanceKm = model.DistanceKm,
            EntryFee = model.EntryFee,
            MinAge = model.MinAge,
            MaxAge = model.MaxAge
        };

        try
        {
            if (model.CategoryId.HasValue)
            {
                await _api.UpdateCategoryAsync(model.CategoryId.Value, request);
                TempData["Success"] = $"\"{model.Name}\" has been updated.";
            }
            else
            {
                await _api.CreateCategoryAsync(model.EventId, request);
                TempData["Success"] = $"\"{model.Name}\" has been added.";
            }

            return RedirectToAction(nameof(Categories), new { id = model.EventId });
        }
        catch (ApiException ex)
        {
            AddApiErrorsToModelState(ex);
            model.ExistingCategories = await SafeGetCategories(model.EventId);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        try
        {
            await _api.DeleteCategoryAsync(categoryId);
            TempData["Success"] = "The category has been removed.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ------------------------------------------------------------ entry list

    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        try
        {
            return View(new EnrolmentListViewModel
            {
                Event = await _api.GetEventAsync(id),
                Enrolments = await _api.GetEventEnrolmentsAsync(id)
            });
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Events));
        }
    }

    // --------------------------------------------------------------- results

    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        try
        {
            return View(await BuildResultModel(id));
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, nameof(Events));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Results(int id, ResultEntryViewModel model)
    {
        if (model.EnrolmentId <= 0)
        {
            ModelState.AddModelError(nameof(model.EnrolmentId), "Choose the participant this result belongs to.");
        }

        if (!ModelState.IsValid || model.ParsedFinishTime is null)
        {
            var reload = await BuildResultModel(id);
            reload.EnrolmentId = model.EnrolmentId;
            reload.FinishTime = model.FinishTime;
            reload.Position = model.Position;
            return View(reload);
        }

        try
        {
            await _api.SaveResultAsync(new SaveResultRequest
            {
                EnrolmentId = model.EnrolmentId,
                FinishTime = model.ParsedFinishTime.Value,
                Position = model.Position!.Value
            });

            TempData["Success"] = "The result has been published.";
            return RedirectToAction(nameof(Results), new { id });
        }
        catch (ApiException ex)
        {
            AddApiErrorsToModelState(ex);
            var reload = await BuildResultModel(id);
            reload.EnrolmentId = model.EnrolmentId;
            reload.FinishTime = model.FinishTime;
            reload.Position = model.Position;
            return View(reload);
        }
    }

    // --------------------------------------------------------------- helpers

    private async Task<ResultEntryViewModel> BuildResultModel(int eventId) => new()
    {
        Event = await _api.GetEventAsync(eventId),
        Enrolments = await _api.GetEventEnrolmentsAsync(eventId)
    };

    private void ApplyExtraRules(EventFormViewModel model)
    {
        foreach (var failure in model.ValidateDate())
        {
            ModelState.AddModelError(failure.MemberNames.FirstOrDefault() ?? string.Empty,
                failure.ErrorMessage ?? "That value is not valid.");
        }
    }

    private async Task UploadBannerIfSupplied(int eventId, IFormFile? banner)
    {
        if (banner is not { Length: > 0 }) return;

        try
        {
            await using var stream = banner.OpenReadStream();
            await _api.UploadEventBannerAsync(eventId, stream, banner.FileName, banner.ContentType);
        }
        catch (ApiException ex)
        {
            // The event itself saved, so a failed image should not look like a failed
            // save. It is reported separately and the organiser can try again.
            TempData["Info"] = $"The event was saved, but the banner was not stored: {ex.UserMessage}";
        }
    }

    private static CreateEventRequest ToCreateRequest(EventFormViewModel model) => new()
    {
        Name = model.Name,
        Description = model.Description,
        EventDate = model.EventDate,
        Location = model.Location,
        City = model.City,
        Province = model.Province,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        DistanceKm = model.DistanceKm,
        EventTypeId = model.EventTypeId,
        MaxParticipants = model.MaxParticipants
    };

    private static UpdateEventRequest ToUpdateRequest(EventFormViewModel model) => new()
    {
        Name = model.Name,
        Description = model.Description,
        EventDate = model.EventDate,
        Location = model.Location,
        City = model.City,
        Province = model.Province,
        Latitude = model.Latitude,
        Longitude = model.Longitude,
        DistanceKm = model.DistanceKm,
        EventTypeId = model.EventTypeId,
        MaxParticipants = model.MaxParticipants,
        Status = model.Status
    };

    private static EventFormViewModel ToViewModel(EventDto dto) => new()
    {
        EventId = dto.EventId,
        Name = dto.Name,
        Description = dto.Description,
        EventDate = dto.EventDate,
        Location = dto.Location,
        City = dto.City,
        Province = dto.Province,
        Latitude = dto.Latitude,
        Longitude = dto.Longitude,
        DistanceKm = dto.DistanceKm,
        EventTypeId = dto.EventTypeId,
        MaxParticipants = dto.MaxParticipants,
        Status = dto.Status,
        ExistingBannerUrl = dto.BannerImageUrl
    };

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
}
