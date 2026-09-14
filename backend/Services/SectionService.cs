using Trippy.Backend.Data.Entities;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

/// <summary>
/// Itineraries are capped at Itinerary.MaxDays (3) sections/days — enforced here since new
/// itineraries auto-create their 3 days already; this only guards against extra manual adds.
/// </summary>
public sealed class SectionService(ISectionRepository sections, IItineraryRepository itineraries) : ISectionService
{
    public Task<IReadOnlyList<ItinerarySection>> ListAsync(Guid? itineraryId, CancellationToken ct = default) =>
        sections.ListAsync(itineraryId, ct);

    public async Task<ItinerarySection> CreateAsync(CreateSectionRequest request, CancellationToken ct = default)
    {
        if (!await itineraries.ExistsAsync(request.ItineraryId, ct))
            throw new KeyNotFoundException("Itinerary not found");

        var existing = await sections.ListAsync(request.ItineraryId, ct);
        if (existing.Count >= Itinerary.MaxDays)
            throw new InvalidOperationException($"Itineraries are capped at {Itinerary.MaxDays} days");

        var seq = request.Sequence ?? await sections.NextSequenceAsync(request.ItineraryId, ct);
        var section = new ItinerarySection
        {
            ItineraryId = request.ItineraryId,
            Date = request.Date,
            Description = request.Description ?? string.Empty,
            Sequence = seq
        };
        sections.Add(section);
        await sections.SaveChangesAsync(ct);
        return (await sections.GetWithItemsAsync(section.Id, ct))!;
    }

    public async Task<ItinerarySection?> UpdateAsync(Guid id, UpdateSectionRequest request, CancellationToken ct = default)
    {
        var section = await sections.GetAsync(id, ct);
        if (section is null) return null;
        if (request.Date is not null) section.Date = request.Date.Value;
        if (request.Description is not null) section.Description = request.Description;
        if (request.Sequence is not null) section.Sequence = request.Sequence.Value;
        await sections.SaveChangesAsync(ct);
        return await sections.GetWithItemsAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var section = await sections.GetAsync(id, ct);
        if (section is null) return false;
        sections.Remove(section);
        await sections.SaveChangesAsync(ct);
        return true;
    }
}
