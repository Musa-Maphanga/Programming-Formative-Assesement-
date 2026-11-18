using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Shared handling of API failures. Without this, every action would need its
/// own try/catch and the user would eventually meet a raw exception page. The
/// rule is simple: a refusal from the API becomes either a form error or a
/// message on the next page, never an unhandled exception.
/// </summary>
public abstract class RaceDayControllerBase : Controller
{
    /// <summary>
    /// Puts the failure on the form the user is looking at, so they can correct
    /// it without losing what they typed.
    /// </summary>
    protected void AddApiErrorsToModelState(ApiException ex)
    {
        if (ex.Details.Count > 0)
        {
            foreach (var detail in ex.Details)
            {
                ModelState.AddModelError(string.Empty, detail);
            }
        }
        else
        {
            ModelState.AddModelError(string.Empty, ex.UserMessage);
        }
    }

    /// <summary>
    /// Used where there is no form to return to. An expired session sends the
    /// user to sign in again rather than showing them a permission error they
    /// can do nothing about.
    /// </summary>
    protected IActionResult HandleApiFailure(ApiException ex, string fallbackAction, string? fallbackController = null)
    {
        if (ex.IsUnauthorised)
        {
            HttpContext.Session.Clear();
            TempData["Error"] = ex.UserMessage;
            return RedirectToAction("Login", "Account");
        }

        TempData["Error"] = ex.UserMessage;
        return fallbackController is null
            ? RedirectToAction(fallbackAction)
            : RedirectToAction(fallbackAction, fallbackController);
    }

    /// <summary>
    /// Rejects anything that is not a reasonably sized image before it is sent
    /// to the API, which saves a round trip and gives a clearer message.
    /// </summary>
    protected bool ValidateImage(IFormFile? file, string fieldName, long maxBytes = 5 * 1024 * 1024)
    {
        if (file is null || file.Length == 0) return true;

        var permitted = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!permitted.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(fieldName, "Upload a JPEG, PNG or WebP image.");
            return false;
        }

        if (file.Length > maxBytes)
        {
            ModelState.AddModelError(fieldName, $"Images need to be smaller than {maxBytes / (1024 * 1024)}MB.");
            return false;
        }

        return true;
    }
}
