using System.ComponentModel.DataAnnotations;

namespace Trippy.Backend.DTOs;

public class ItinerarySummary
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
}

public class ItineraryDetail
{
    [Required] public Guid Id { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    public List<SectionRead> Sections { get; set; } = [];
}

/// <summary>
/// Itineraries are always Itinerary.MaxDays (3) days long. The caller only picks a start
/// date; end_date and the day sections are derived/created automatically.
/// </summary>
public class ItineraryCreate
{
    [Required] public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    [Required] public DateOnly StartDate { get; set; }
}

public class ItineraryUpdate
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary>Changing this shifts all day sections to keep the fixed 3-day span.</summary>
    public string? StartDate { get; set; }
}
