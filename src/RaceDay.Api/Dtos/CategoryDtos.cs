using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

/// <summary>Body for adding or changing a category on an event.</summary>
public class SaveCategoryRequest
{
    /// <summary>The name of the distance or class, unique within the event.</summary>
    /// <example>21.1km Half Marathon</example>
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <example>21.1</example>
    [Required, Range(0.01, 9999.99)]
    public decimal? DistanceKm { get; set; }

    /// <summary>Youngest age allowed on race day. Leave empty for no lower limit.</summary>
    /// <example>16</example>
    [Range(0, 120)]
    public int? MinAge { get; set; }

    /// <summary>Oldest age allowed on race day. Leave empty for no upper limit.</summary>
    /// <example>49</example>
    [Range(0, 120)]
    public int? MaxAge { get; set; }

    /// <example>250.00</example>
    [Required, Range(0, 999999.99)]
    public decimal? EntryFee { get; set; }
}

/// <summary>A category as returned by the API.</summary>
public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal EntryFee { get; set; }

    /// <summary>How many people are currently entered in this category (withdrawals excluded).</summary>
    public int EnrolledCount { get; set; }
}
