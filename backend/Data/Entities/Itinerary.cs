namespace Trippy.Backend.Data.Entities;

/// <summary>
/// Trip plan. Sections are ordered days; items hang under sections and reference Places.
/// Itineraries are always exactly <see cref="MaxDays"/> days long, starting at StartDate.
/// </summary>
public class Itinerary
{
    public const int MaxDays = 3;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ICollection<ItinerarySection> Sections { get; set; } = [];
}
