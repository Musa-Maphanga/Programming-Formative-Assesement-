using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// The participant's own corner of the platform: what they have entered and
/// what they have run.
/// </summary>
[RequireRole(Roles.Participant)]
public class ParticipantController : RaceDayControllerBase
{
    private readonly IRaceDayApiClient _api;

    public ParticipantController(IRaceDayApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> MyEvents()
    {
        try
        {
            return View(new MyEventsViewModel { Enrolments = await _api.GetMyEnrolmentsAsync() });
        }
        catch (ApiException ex)
        {
            if (ex.IsUnauthorised) return HandleApiFailure(ex, "Login", "Account");
            TempData["Error"] = ex.UserMessage;
            return View(new MyEventsViewModel());
        }
        catch (HttpRequestException)
        {
            TempData["Error"] = "The RaceDay service is not responding, so your entries cannot be loaded.";
            return View(new MyEventsViewModel());
        }
    }

    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        try
        {
            return View(new MyResultsViewModel { Results = await _api.GetMyResultsAsync() });
        }
        catch (ApiException ex)
        {
            if (ex.IsUnauthorised) return HandleApiFailure(ex, "Login", "Account");
            TempData["Error"] = ex.UserMessage;
            return View(new MyResultsViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int enrolmentId)
    {
        try
        {
            await _api.WithdrawAsync(enrolmentId);
            TempData["Success"] = "You have withdrawn, and your place has been released.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.UserMessage;
        }

        return RedirectToAction(nameof(MyEvents));
    }
}
