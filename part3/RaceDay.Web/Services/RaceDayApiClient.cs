using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// The single implementation of <see cref="IRaceDayApiClient"/>. Every route the
/// Part 2 API exposes is named here and nowhere else, so if a route or a payload
/// changes in Part 2, this file is the only one that has to follow it.
/// </summary>
public class RaceDayApiClient : IRaceDayApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RaceDayApiClient> _logger;
    private readonly JsonSerializerOptions _json;

    public RaceDayApiClient(HttpClient http, ILogger<RaceDayApiClient> logger, JsonSerializerOptions json)
    {
        _http = http;
        _logger = logger;
        _json = json;
    }

    // ------------------------------------------------------------- identity

    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => PostAsync<LoginResponse>("api/auth/login", request, ct);

    /// <summary>
    /// Registration returns the new profile rather than a token, so the caller signs
    /// the user in afterwards with the password they just chose.
    /// </summary>
    public Task<UserProfile> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        => PostAsync<UserProfile>("api/auth/register", request, ct);

    /// <summary>Ends the server-side session that the token points at.</summary>
    public Task LogoutAsync(CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, "api/auth/logout", null, ct);

    // -------------------------------------------------------------- profile

    public Task<UserProfile> GetMyProfileAsync(CancellationToken ct = default)
        => GetAsync<UserProfile>("api/users/me", ct);

    public Task<UserProfile> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
        => SendAsync<UserProfile>(HttpMethod.Put, "api/users/me", request, ct);

    public Task<string> UploadProfilePictureAsync(
        Stream content, string fileName, string contentType, CancellationToken ct = default)
        => UploadAsync("api/users/me/profile-picture", content, fileName, contentType, ct);

    // ------------------------------------------------------- reference data

    public Task<List<EventTypeDto>> GetEventTypesAsync(CancellationToken ct = default)
        => GetAsync<List<EventTypeDto>>("api/event-types", ct);

    // --------------------------------------------------------------- events

    /// <summary>
    /// The API returns a plain list and has no free-text parameter, so the name and
    /// venue search is applied here over what comes back.
    /// </summary>
    public async Task<List<EventDto>> GetEventsAsync(EventSearch search, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(search.Type)) query.Add($"type={Uri.EscapeDataString(search.Type)}");
        if (!string.IsNullOrWhiteSpace(search.City)) query.Add($"city={Uri.EscapeDataString(search.City)}");
        if (!string.IsNullOrWhiteSpace(search.Province)) query.Add($"province={Uri.EscapeDataString(search.Province)}");
        if (!string.IsNullOrWhiteSpace(search.Status)) query.Add($"status={Uri.EscapeDataString(search.Status)}");
        if (search.From.HasValue) query.Add($"from={search.From:yyyy-MM-dd}");
        if (search.To.HasValue) query.Add($"to={search.To:yyyy-MM-dd}");

        var route = query.Count == 0 ? "api/events" : $"api/events?{string.Join("&", query)}";
        var events = await GetAsync<List<EventDto>>(route, ct);

        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            var term = search.Query.Trim();
            events = events.Where(e =>
                    e.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || e.Location.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || e.City.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return events;
    }

    public Task<EventDto> GetEventAsync(int eventId, CancellationToken ct = default)
        => GetAsync<EventDto>($"api/events/{eventId}", ct);

    public Task<List<EventDto>> GetMyEventsAsync(CancellationToken ct = default)
        => GetAsync<List<EventDto>>("api/users/me/events", ct);

    public Task<EventDto> CreateEventAsync(CreateEventRequest request, CancellationToken ct = default)
        => PostAsync<EventDto>("api/events", request, ct);

    public Task<EventDto> UpdateEventAsync(int eventId, UpdateEventRequest request, CancellationToken ct = default)
        => SendAsync<EventDto>(HttpMethod.Put, $"api/events/{eventId}", request, ct);

    /// <summary>
    /// The API has no status-only endpoint: status travels with the rest of the event
    /// on a PUT. So the current event is read first and sent back with the new status,
    /// which also means nothing else on the event is accidentally blanked.
    /// </summary>
    public async Task<EventDto> SetEventStatusAsync(int eventId, string status, CancellationToken ct = default)
    {
        var current = await GetEventAsync(eventId, ct);

        return await UpdateEventAsync(eventId, new UpdateEventRequest
        {
            Name = current.Name,
            Description = current.Description,
            EventDate = current.EventDate,
            Location = current.Location,
            City = current.City,
            Province = current.Province,
            Latitude = current.Latitude,
            Longitude = current.Longitude,
            DistanceKm = current.DistanceKm,
            EventTypeId = current.EventTypeId,
            MaxParticipants = current.MaxParticipants,
            Status = status
        }, ct);
    }

    public Task DeleteEventAsync(int eventId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/events/{eventId}", null, ct);

    public Task<string> UploadEventBannerAsync(
        int eventId, Stream content, string fileName, string contentType, CancellationToken ct = default)
        => UploadAsync($"api/events/{eventId}/banner", content, fileName, contentType, ct);

    // ----------------------------------------------------------- categories

    public Task<List<CategoryDto>> GetCategoriesAsync(int eventId, CancellationToken ct = default)
        => GetAsync<List<CategoryDto>>($"api/events/{eventId}/categories", ct);

    public Task<CategoryDto> CreateCategoryAsync(int eventId, SaveCategoryRequest request, CancellationToken ct = default)
        => PostAsync<CategoryDto>($"api/events/{eventId}/categories", request, ct);

    public Task<CategoryDto> UpdateCategoryAsync(int categoryId, SaveCategoryRequest request, CancellationToken ct = default)
        => SendAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", request, ct);

    public Task DeleteCategoryAsync(int categoryId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/categories/{categoryId}", null, ct);

    // ----------------------------------------------------------- enrolments

    public Task<EnrolmentDto> EnterEventAsync(CreateEnrolmentRequest request, CancellationToken ct = default)
        => PostAsync<EnrolmentDto>("api/enrolments", request, ct);

    public Task<List<EnrolmentDto>> GetMyEnrolmentsAsync(CancellationToken ct = default)
        => GetAsync<List<EnrolmentDto>>("api/users/me/enrolments", ct);

    public Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId, CancellationToken ct = default)
        => GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments", ct);

    public Task WithdrawAsync(int enrolmentId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"api/enrolments/{enrolmentId}/withdraw", null, ct);

    // -------------------------------------------------------------- results

    public Task<List<ResultDto>> GetMyResultsAsync(CancellationToken ct = default)
        => GetAsync<List<ResultDto>>("api/users/me/results", ct);

    public Task<List<ResultDto>> GetEventResultsAsync(int eventId, CancellationToken ct = default)
        => GetAsync<List<ResultDto>>($"api/events/{eventId}/results", ct);

    public Task SaveResultAsync(SaveResultRequest request, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, "api/results", request, ct);

    public Task UpdateResultAsync(int resultId, SaveResultRequest request, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, $"api/results/{resultId}", request, ct);

    // ------------------------------------------------------------- plumbing
    // Everything below turns an HTTP exchange into either a typed object or an
    // ApiException. No caller ever sees an HttpResponseMessage.

    private async Task<T> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await _http.GetAsync(route, ct);
        return await ReadAsync<T>(response, route, ct);
    }

    private Task<T> PostAsync<T>(string route, object? body, CancellationToken ct)
        => SendAsync<T>(HttpMethod.Post, route, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, route, body);
        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<T>(response, route, ct);
    }

    private async Task SendAsync(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        using var request = BuildRequest(method, route, body);
        using var response = await _http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, route, ct);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string route, object? body)
    {
        var request = new HttpRequestMessage(method, route);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: _json);
        }
        return request;
    }

    /// <summary>
    /// Sends a file to the API as multipart form data under the field name "file",
    /// which is what both upload endpoints expect. Image handling is the API's job:
    /// this application holds no Azure credentials and never contacts Blob Storage.
    /// </summary>
    private async Task<string> UploadAsync(
        string route, Stream content, string fileName, string contentType, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        using var response = await _http.PostAsync(route, form, ct);
        await EnsureSuccessAsync(response, route, ct);

        var payload = await response.Content.ReadFromJsonAsync<UploadResponse>(_json, ct);
        return payload?.Url ?? "";
    }

    private async Task<T> ReadAsync<T>(HttpResponseMessage response, string route, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, route, ct);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default!;
        }

        var value = await response.Content.ReadFromJsonAsync<T>(_json, ct);
        if (value is null)
        {
            throw new ApiException(response.StatusCode, "The RaceDay service returned an empty response.");
        }
        return value;
    }

    /// <summary>
    /// The API answers failures as ProblemDetails, and model validation failures as
    /// ValidationProblemDetails with a per-field errors dictionary. Both are unpacked
    /// here so a controller can show the user which field was wrong.
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, string route, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var message = "";
        var details = new List<string>();

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(_json, ct);
            message = problem?.Detail ?? problem?.Title ?? "";

            if (problem?.Errors is { Count: > 0 })
            {
                details.AddRange(problem.Errors.SelectMany(field => field.Value));
            }
        }
        catch (Exception)
        {
            // The API did not return JSON. The status code alone will have to do.
        }

        _logger.LogWarning("API call to {Route} failed with {Status}: {Message}",
            route, (int)response.StatusCode, message);

        throw new ApiException(response.StatusCode, message, details);
    }

    /// <summary>Covers both ProblemDetails and ValidationProblemDetails.</summary>
    private class ApiProblemDetails
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, List<string>>? Errors { get; set; }
    }
}
