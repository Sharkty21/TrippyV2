using Trippy.Backend.Data.Entities;
using Trippy.Backend.DTOs;

namespace Trippy.Backend.Interfaces;

public interface IPlaceService
{
    Task<IReadOnlyList<Place>> ListAsync(string? city, string? type, CancellationToken ct = default);
    Task<Place?> GetAsync(string id, CancellationToken ct = default);
}

public interface IItineraryService
{
    Task<IReadOnlyList<Itinerary>> ListAsync(CancellationToken ct = default);
    Task<Itinerary?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Itinerary> CreateAsync(CreateItineraryRequest request, CancellationToken ct = default);
    Task<Itinerary?> UpdateAsync(Guid id, UpdateItineraryRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ISectionService
{
    Task<IReadOnlyList<ItinerarySection>> ListAsync(Guid? itineraryId, CancellationToken ct = default);
    Task<ItinerarySection> CreateAsync(CreateSectionRequest request, CancellationToken ct = default);
    Task<ItinerarySection?> UpdateAsync(Guid id, UpdateSectionRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IItemService
{
    Task<ItineraryItem> CreateAsync(CreateItemRequest request, CancellationToken ct = default);
    Task<ItineraryItem?> UpdateAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItineraryItem>> ReorderAsync(Guid sectionId, IReadOnlyList<Guid> itemIds, CancellationToken ct = default);
}

public interface IPendingActionService
{
    Task<PendingAction?> GetAsync(Guid id, CancellationToken ct = default);
    Task<PendingAction?> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PendingAction?> DenyAsync(Guid id, CancellationToken ct = default);
}
