namespace Trippy.Backend.Data.Entities;

public class ItinerarySection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ItineraryId { get; set; }
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Sequence { get; set; }

    public Itinerary? Itinerary { get; set; }
    public ICollection<ItineraryItem> Items { get; set; } = [];
}
