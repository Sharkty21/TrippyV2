namespace Trippy.Backend.Data.Entities;

public class ItineraryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public string PlaceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Sequence { get; set; }
    /// <summary>Planned local start time for this stop (e.g. "09:30"). Null if unscheduled.</summary>
    public TimeOnly? StartTime { get; set; }

    public ItinerarySection? Section { get; set; }
    public Place? Place { get; set; }
}
