using System.ComponentModel.DataAnnotations;

namespace Trippy.Backend.DTOs;

public class SectionRead
{
    [Required] public Guid Id { get; set; }
    [Required] public Guid ItineraryId { get; set; }
    [Required] public DateOnly Date { get; set; }
    [Required] public string Description { get; set; } = "";
    [Required] public int Sequence { get; set; }
    public List<ItemRead> Items { get; set; } = [];
}

public class SectionCreate
{
    [Required] public Guid ItineraryId { get; set; }
    [Required] public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
    public int? Sequence { get; set; }
}

public class SectionUpdate
{
    public string? Date { get; set; }
    public string? Description { get; set; }
    public int? Sequence { get; set; }
}
