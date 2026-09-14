using System.ComponentModel.DataAnnotations;

namespace Trippy.Backend.DTOs;

// Schema names match Orval/OpenAPI titles. Non-nullable props are [Required]
// so swagger.json marks them required (needed for strict TS clients).

public class PlaceRead
{
    [Required] public string Id { get; set; } = "";
    [Required] public string Name { get; set; } = "";
    [Required] public string Type { get; set; } = "";
    [Required] public string City { get; set; } = "";
    [Required] public string Region { get; set; } = "";
    public string? Neighborhood { get; set; }
    [Required] public string Description { get; set; } = "";
    [Required] public double Latitude { get; set; }
    [Required] public double Longitude { get; set; }
    public string? Hours { get; set; }
    public int? DurationMinutes { get; set; }
    public string? PriceRange { get; set; }
    public double? Rating { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? SeasonalNotes { get; set; }
    public bool BookingRequired { get; set; }
}
