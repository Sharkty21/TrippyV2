namespace Trippy.Backend.Data.Entities;

/// <summary>
/// Point of interest. Seeded from Data/italy.json — add rows there or insert via EF.
/// </summary>
public class Place
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string? Neighborhood { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Hours { get; set; }
    public int? DurationMinutes { get; set; }
    public string? PriceRange { get; set; }
    public double? Rating { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? SeasonalNotes { get; set; }
    public bool BookingRequired { get; set; }

    public ICollection<ItineraryItem> Items { get; set; } = [];
}
