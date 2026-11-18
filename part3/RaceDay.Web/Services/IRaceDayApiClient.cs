using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Services;

/// <summary>
/// Every piece of data this web application shows comes through this interface.
/// Nothing else in the project opens a connection, builds a query or knows an
/// API route. Controllers depend on this abstraction, which is what keeps the
/// MVC project free of data access and business logic.
/// </summary>
public interface IRaceDayApiClient
{
    // identity
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserProfile> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task LogoutAsync(CancellationToken ct = default);

    // profile
    Task<UserProfile> GetMyProfileAsync(CancellationToken ct = default);
    Task<UserProfile> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<string> UploadProfilePictureAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    // reference data
    Task<List<EventTypeDto>> GetEventTypesAsync(CancellationToken ct = default);

    // events
    Task<List<EventDto>> GetEventsAsync(EventSearch search, CancellationToken ct = default);
    Task<EventDto> GetEventAsync(int eventId, CancellationToken ct = default);
    Task<List<EventDto>> GetMyEventsAsync(CancellationToken ct = default);
    Task<EventDto> CreateEventAsync(CreateEventRequest request, CancellationToken ct = default);
    Task<EventDto> UpdateEventAsync(int eventId, UpdateEventRequest request, CancellationToken ct = default);
    Task<EventDto> SetEventStatusAsync(int eventId, string status, CancellationToken ct = default);
    Task DeleteEventAsync(int eventId, CancellationToken ct = default);
    Task<string> UploadEventBannerAsync(int eventId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    // categories
    Task<List<CategoryDto>> GetCategoriesAsync(int eventId, CancellationToken ct = default);
    Task<CategoryDto> CreateCategoryAsync(int eventId, SaveCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateCategoryAsync(int categoryId, SaveCategoryRequest request, CancellationToken ct = default);
    Task DeleteCategoryAsync(int categoryId, CancellationToken ct = default);

    // enrolments
    Task<EnrolmentDto> EnterEventAsync(CreateEnrolmentRequest request, CancellationToken ct = default);
    Task<List<EnrolmentDto>> GetMyEnrolmentsAsync(CancellationToken ct = default);
    Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId, CancellationToken ct = default);
    Task WithdrawAsync(int enrolmentId, CancellationToken ct = default);

    // results
    Task<List<ResultDto>> GetMyResultsAsync(CancellationToken ct = default);
    Task<List<ResultDto>> GetEventResultsAsync(int eventId, CancellationToken ct = default);
    Task SaveResultAsync(SaveResultRequest request, CancellationToken ct = default);
    Task UpdateResultAsync(int resultId, SaveResultRequest request, CancellationToken ct = default);
}

/// <summary>
/// Filters for the race listing. Everything except <see cref="Query"/> is passed
/// to the API; the free-text search is applied here, because the API has no
/// parameter for it.
/// </summary>
public class EventSearch
{
    public string? Query { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Status { get; set; }
}
