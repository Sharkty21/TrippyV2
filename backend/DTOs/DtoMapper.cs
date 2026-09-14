using System.Text.Json;
using Trippy.Backend.Data.Entities;

namespace Trippy.Backend.DTOs;

/// <summary>
/// Domain entity → DTO helpers. Keep controllers thin; extend here when response shapes grow.
/// </summary>
public static class DtoMapper
{
    public static PlaceRead ToDto(this Place p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Type = p.Type,
        City = p.City,
        Region = p.Region,
        Neighborhood = p.Neighborhood,
        Description = p.Description,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        Hours = p.Hours,
        DurationMinutes = p.DurationMinutes,
        PriceRange = p.PriceRange,
        Rating = p.Rating,
        Tags = p.Tags,
        SeasonalNotes = p.SeasonalNotes,
        BookingRequired = p.BookingRequired
    };

    public static ItemRead ToDto(this ItineraryItem item) => new()
    {
        Id = item.Id,
        SectionId = item.SectionId,
        PlaceId = item.PlaceId,
        Description = item.Description,
        Sequence = item.Sequence,
        StartTime = item.StartTime?.ToString("HH:mm"),
        Place = item.Place?.ToDto()
    };

    public static SectionRead ToDto(this ItinerarySection section) => new()
    {
        Id = section.Id,
        ItineraryId = section.ItineraryId,
        Date = section.Date,
        Description = section.Description,
        Sequence = section.Sequence,
        Items = section.Items.OrderBy(i => i.Sequence).Select(i => i.ToDto()).ToList()
    };

    public static ItinerarySummary ToSummary(this Itinerary i) => new()
    {
        Id = i.Id,
        Name = i.Name,
        Description = i.Description,
        StartDate = i.StartDate,
        EndDate = i.EndDate
    };

    public static ItineraryDetail ToDetail(this Itinerary i) => new()
    {
        Id = i.Id,
        Name = i.Name,
        Description = i.Description,
        StartDate = i.StartDate,
        EndDate = i.EndDate,
        Sections = i.Sections.OrderBy(s => s.Sequence).Select(s => s.ToDto()).ToList()
    };

    public static PendingActionRead ToDto(this PendingAction action)
    {
        return new PendingActionRead
        {
            Id = action.Id,
            ConversationId = action.ConversationId,
            ToolName = action.ToolName,
            Payload = ParseJson(action.PayloadJson),
            Summary = action.Summary,
            Status = action.Status.ToString().ToLowerInvariant(),
            CreatedAt = action.CreatedAt,
            Result = action.ResultJson is null ? null : ParseJson(action.ResultJson)
        };
    }

    private static object ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.Clone();
    }
}
