using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Settings for the Azure Storage account, bound from the AzureStorage section.</summary>
public class AzureStorageSettings
{
    public const string SectionName = "AzureStorage";

    /// <summary>Connection string for the storage account. Leave empty to turn uploads off.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Container for event banner images.</summary>
    public string EventBannersContainer { get; set; } = "event-banners";

    /// <summary>Container for profile pictures.</summary>
    public string ProfilePicturesContainer { get; set; } = "profile-pictures";

    /// <summary>Largest image accepted, in megabytes.</summary>
    public int MaxFileSizeMb { get; set; } = 5;
}

/// <summary>Raised when an upload is rejected for a reason the user can fix.</summary>
public class ImageRejectedException : Exception
{
    public ImageRejectedException(string message) : base(message) { }
}

/// <summary>Raised when storage itself is unreachable or not configured.</summary>
public class ImageStorageUnavailableException : Exception
{
    public ImageStorageUnavailableException(string message) : base(message) { }
}

public interface IImageStorage
{
    /// <summary>True when a storage connection string has been configured.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Stores an image and returns the public URL to it. Containers are created on first use.
    /// </summary>
    Task<string> UploadAsync(string container, IFormFile file, string blobName, CancellationToken ct = default);

    /// <summary>Removes a previously stored image. Missing blobs are ignored.</summary>
    Task DeleteAsync(string url, CancellationToken ct = default);
}

/// <summary>
/// Stores uploaded images in Azure Blob Storage. The MVC front end never talks to Azure: it posts
/// the file to this API, the API stores it, and the API hands back the URL that gets saved against
/// the event or the user.
/// </summary>
public class AzureBlobImageStorage : IImageStorage
{
    private static readonly string[] PermittedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private static readonly string[] PermittedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly AzureStorageSettings _settings;
    private readonly ILogger<AzureBlobImageStorage> _logger;

    public AzureBlobImageStorage(Microsoft.Extensions.Options.IOptions<AzureStorageSettings> settings,
        ILogger<AzureBlobImageStorage> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ConnectionString);

    public async Task<string> UploadAsync(string container, IFormFile file, string blobName, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new ImageStorageUnavailableException(
                "Image uploads are not configured on this server. Set AzureStorage:ConnectionString.");

        Validate(file);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fullName = $"{blobName}{extension}";

        try
        {
            var client = new BlobContainerClient(_settings.ConnectionString, container);

            // Blob-level public access means the browser can load the image straight from the URL.
            await client.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

            var blob = client.GetBlobClient(fullName);

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType }
            }, ct);

            return blob.Uri.ToString();
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob Storage refused an upload to {Container}", container);
            throw new ImageStorageUnavailableException(
                "The image could not be stored right now. Please try again shortly.");
        }
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var uri = new Uri(url);

            // A blob URL is https://account.blob.core.windows.net/<container>/<blob>
            var segments = uri.AbsolutePath.Trim('/').Split('/', 2);
            if (segments.Length != 2) return;

            var client = new BlobContainerClient(_settings.ConnectionString, segments[0]);
            await client.GetBlobClient(segments[1]).DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is UriFormatException or Azure.RequestFailedException)
        {
            // Replacing an image should not fail because the old one could not be tidied up.
            _logger.LogWarning(ex, "Could not delete the previous image at {Url}", url);
        }
    }

    private void Validate(IFormFile file)
    {
        if (file.Length == 0)
            throw new ImageRejectedException("The uploaded file is empty.");

        var maxBytes = (long)_settings.MaxFileSizeMb * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new ImageRejectedException($"Images need to be smaller than {_settings.MaxFileSizeMb}MB.");

        if (!PermittedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ImageRejectedException("Upload a JPEG, PNG or WebP image.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!PermittedExtensions.Contains(extension))
            throw new ImageRejectedException("Upload a file ending in .jpg, .jpeg, .png or .webp.");
    }
}
