using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Models.ViewModels;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Registration, sign-in, sign-out and profile. No password is ever stored or
/// checked here: credentials are posted to the Part 2 API, which answers with a
/// token, and the token is what the session holds.
/// </summary>
public class AccountController : RaceDayControllerBase
{
    private readonly IRaceDayApiClient _api;

    public AccountController(IRaceDayApiClient api) => _api = api;

    // ----------------------------------------------------------------- login

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await SignInAsync(model.Email, model.Password);

            // Only local paths are honoured, so a crafted returnUrl cannot send the
            // user to another site after signing in.
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError(string.Empty,
                ex.IsUnauthorised
                    ? "That email address and password do not match an account."
                    : ex.UserMessage);
            return View(model);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty,
                "The RaceDay service is not responding. Please try again in a moment.");
            return View(model);
        }
    }

    // -------------------------------------------------------------- register

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _api.RegisterAsync(new RegisterRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Password = model.Password,
                Role = model.Role,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                City = model.City
            });

            // Registration returns the new profile rather than a token, so the user is
            // signed in straight afterwards with the password they just chose. Without
            // this they would have to type it again on the very next screen.
            await SignInAsync(model.Email, model.Password);

            TempData["Success"] = $"Welcome to RaceDay, {model.FirstName}. Your account is ready.";
            return RedirectToAction("Index", "Home");
        }
        catch (ApiException ex)
        {
            if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "An account already exists for that email address.");
            }
            else
            {
                AddApiErrorsToModelState(ex);
            }
            return View(model);
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(string.Empty,
                "The RaceDay service is not responding. Please try again in a moment.");
            return View(model);
        }
    }

    // ---------------------------------------------------------------- logout

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // The API keeps a server-side session behind the token, so signing out tells it
        // to end that session as well. If the call fails the local session is still
        // cleared, because the user asked to be signed out.
        try
        {
            if (HttpContext.Session.IsSignedIn()) await _api.LogoutAsync();
        }
        catch (Exception)
        {
            // Nothing useful to tell the user here.
        }

        HttpContext.Session.Clear();
        TempData["Info"] = "You have been signed out.";
        return RedirectToAction("Index", "Home");
    }

    // --------------------------------------------------------------- profile

    [HttpGet]
    [RequireRole]
    public async Task<IActionResult> Profile()
    {
        try
        {
            var profile = await _api.GetMyProfileAsync();
            return View(ToViewModel(profile));
        }
        catch (ApiException ex)
        {
            return HandleApiFailure(ex, "Index", "Home");
        }
    }

    [HttpPost]
    [RequireRole]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ValidateImage(model.ProfilePicture, nameof(model.ProfilePicture)) || !ModelState.IsValid)
        {
            model.Profile = await SafeGetProfile() ?? new UserProfile();
            return View(model);
        }

        try
        {
            await _api.UpdateMyProfileAsync(new UpdateProfileRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                City = model.City
            });

            // The picture goes to the API as a file. The API puts it in Azure Blob
            // Storage; this application never touches Azure itself.
            if (model.ProfilePicture is { Length: > 0 })
            {
                await using var stream = model.ProfilePicture.OpenReadStream();
                await _api.UploadProfilePictureAsync(
                    stream, model.ProfilePicture.FileName, model.ProfilePicture.ContentType);
            }

            // The name in the masthead comes from the session, so it has to be
            // refreshed or the page would still greet them by the old name.
            var session = HttpContext.Session;
            session.SignIn(
                session.GetToken()!,
                session.GetUserId() ?? 0,
                session.GetRole() ?? "",
                model.FirstName);

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }
        catch (ApiException ex)
        {
            AddApiErrorsToModelState(ex);
            model.Profile = await SafeGetProfile() ?? new UserProfile();
            return View(model);
        }
    }

    // ---------------------------------------------------------------- denied

    /// <summary>
    /// Shown when a signed-in user reaches a page meant for the other role. Bouncing
    /// them to the sign-in page would be misleading, since they are already signed in.
    /// </summary>
    [HttpGet]
    public IActionResult Denied() => View();

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Signs in and fills the session. The login response carries only the token, the
    /// user id and the role, so the display name is read from the profile endpoint
    /// afterwards - which works because the token is already in session by then.
    /// </summary>
    private async Task SignInAsync(string email, string password)
    {
        var auth = await _api.LoginAsync(new LoginRequest { Email = email, Password = password });

        HttpContext.Session.SignIn(auth.Token, auth.UserId, auth.Role, "My account");

        try
        {
            var profile = await _api.GetMyProfileAsync();
            HttpContext.Session.SignIn(auth.Token, auth.UserId, auth.Role, profile.FirstName);
        }
        catch (ApiException)
        {
            // The sign-in itself worked, so a failure to read the name is not worth
            // stopping for. The masthead falls back to "My account".
        }
    }

    private async Task<UserProfile?> SafeGetProfile()
    {
        try { return await _api.GetMyProfileAsync(); }
        catch (ApiException) { return null; }
    }

    private static ProfileViewModel ToViewModel(UserProfile profile) => new()
    {
        Profile = profile,
        FirstName = profile.FirstName,
        LastName = profile.LastName,
        PhoneNumber = profile.PhoneNumber,
        DateOfBirth = profile.DateOfBirth,
        Gender = profile.Gender,
        City = profile.City
    };
}
