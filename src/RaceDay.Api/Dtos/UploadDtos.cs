namespace RaceDay.Api.Dtos;

/// <summary>Returned after an image is stored, so the client knows where to load it from.</summary>
public class UploadResponse
{
    /// <summary>Public URL of the stored image in Azure Blob Storage.</summary>
    /// <example>https://raceday.blob.core.windows.net/event-banners/event-1.jpg</example>
    public string Url { get; set; } = string.Empty;
}
