using Trippy.Backend.Data.Entities;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

public sealed class ItemService(
    IItemRepository items,
    ISectionRepository sections,
    IPlaceRepository places) : IItemService
{
    public async Task<ItineraryItem> CreateAsync(CreateItemRequest request, CancellationToken ct = default)
    {
        if (await sections.GetAsync(request.SectionId, ct) is null)
            throw new KeyNotFoundException("Section not found");
        if (!await places.ExistsAsync(request.PlaceId, ct))
            throw new KeyNotFoundException("Place not found");

        var seq = request.Sequence ?? await items.NextSequenceAsync(request.SectionId, ct);
        var item = new ItineraryItem
        {
            SectionId = request.SectionId,
            PlaceId = request.PlaceId,
            Description = request.Description ?? string.Empty,
            Sequence = seq,
            StartTime = request.StartTime
        };
        items.Add(item);
        await items.SaveChangesAsync(ct);
        return (await items.GetWithPlaceAsync(item.Id, ct))!;
    }

    public async Task<ItineraryItem?> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct);
        if (item is null) return null;
        if (request.PlaceId is not null)
        {
            if (!await places.ExistsAsync(request.PlaceId, ct))
                throw new KeyNotFoundException("Place not found");
            item.PlaceId = request.PlaceId;
        }
        if (request.Description is not null) item.Description = request.Description;
        if (request.Sequence is not null) item.Sequence = request.Sequence.Value;
        if (request.ClearStartTime) item.StartTime = null;
        else if (request.StartTime is not null) item.StartTime = request.StartTime;
        await items.SaveChangesAsync(ct);
        return await items.GetWithPlaceAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await items.GetAsync(id, ct);
        if (item is null) return false;
        items.Remove(item);
        await items.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ItineraryItem>> ReorderAsync(Guid sectionId, IReadOnlyList<Guid> itemIds, CancellationToken ct = default)
    {
        if (await sections.GetAsync(sectionId, ct) is null)
            throw new KeyNotFoundException("Section not found");

        for (var i = 0; i < itemIds.Count; i++)
        {
            var itemId = itemIds[i];
            var item = await items.GetAsync(itemId, ct)
                       ?? throw new ArgumentException($"Invalid item {itemId}");
            if (item.SectionId != sectionId)
                throw new ArgumentException($"Invalid item {itemId}");
            item.Sequence = i;
        }

        await items.SaveChangesAsync(ct);
        return await items.ListBySectionAsync(sectionId, ct);
    }
}
