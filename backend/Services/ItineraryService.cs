using Trippy.Backend.Data.Entities;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

/// <summary>
/// Itineraries are always fixed at <see cref="Itinerary.MaxDays"/> (3) days. Creating one
/// auto-generates a section per day; changing the start date shifts all section dates so
/// the trip stays anchored to the new start while keeping the same length.
/// </summary>
public sealed class ItineraryService(IItineraryRepository itineraries, ISectionRepository sections) : IItineraryService
{
    public Task<IReadOnlyList<Itinerary>> ListAsync(CancellationToken ct = default) =>
        itineraries.ListAsync(ct);

    public Task<Itinerary?> GetAsync(Guid id, CancellationToken ct = default) =>
        itineraries.GetDetailAsync(id, ct);

    public async Task<Itinerary> CreateAsync(CreateItineraryRequest request, CancellationToken ct = default)
    {
        var itin = new Itinerary
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            StartDate = request.StartDate,
            EndDate = request.StartDate.AddDays(Itinerary.MaxDays - 1)
        };
        itineraries.Add(itin);
        await itineraries.SaveChangesAsync(ct);

        for (var day = 0; day < Itinerary.MaxDays; day++)
        {
            sections.Add(new ItinerarySection
            {
                ItineraryId = itin.Id,
                Date = request.StartDate.AddDays(day),
                Description = string.Empty,
                Sequence = day
            });
        }
        await sections.SaveChangesAsync(ct);

        return (await itineraries.GetDetailAsync(itin.Id, ct))!;
    }

    public async Task<Itinerary?> UpdateAsync(Guid id, UpdateItineraryRequest request, CancellationToken ct = default)
    {
        var itin = await itineraries.GetAsync(id, ct);
        if (itin is null) return null;

        if (request.Name is not null) itin.Name = request.Name;
        if (request.Description is not null) itin.Description = request.Description;

        if (request.StartDate is not null && request.StartDate.Value != itin.StartDate)
        {
            var dayShift = request.StartDate.Value.DayNumber - itin.StartDate.DayNumber;
            itin.StartDate = request.StartDate.Value;
            itin.EndDate = itin.StartDate.AddDays(Itinerary.MaxDays - 1);

            var daySectionIds = (await sections.ListAsync(id, ct)).Select(s => s.Id).ToList();
            foreach (var sectionId in daySectionIds)
            {
                var tracked = await sections.GetAsync(sectionId, ct);
                if (tracked is not null)
                    tracked.Date = tracked.Date.AddDays(dayShift);
            }
            await sections.SaveChangesAsync(ct);
        }

        await itineraries.SaveChangesAsync(ct);
        return await itineraries.GetDetailAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var itin = await itineraries.GetAsync(id, ct);
        if (itin is null) return false;
        itineraries.Remove(itin);
        await itineraries.SaveChangesAsync(ct);
        return true;
    }
}
