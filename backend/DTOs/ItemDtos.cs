using System.ComponentModel.DataAnnotations;

namespace Trippy.Backend.DTOs;

public class ItemRead
{
    [Required] public Guid Id { get; set; }
    [Required] public Guid SectionId { get; set; }
    [Required] public string PlaceId { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public int Sequence { get; set; }
    /// <summary>Planned local start time, e.g. "09:30". Null if unscheduled.</summary>
    public string? StartTime { get; set; }
    public PlaceRead? Place { get; set; }
}

public class ItemCreate
{
    [Required] public Guid SectionId { get; set; }
    [Required] public string PlaceId { get; set; } = "";
    public string Description { get; set; } = "";
    public int? Sequence { get; set; }
    public string? StartTime { get; set; }
}

public class ItemUpdate
{
    public string? PlaceId { get; set; }
    public string? Description { get; set; }
    public int? Sequence { get; set; }
    /// <summary>Set to an empty string to clear the start time.</summary>
    public string? StartTime { get; set; }
}

public class ItemReorderRequest
{
    [Required] public List<Guid> ItemIds { get; set; } = [];
}
