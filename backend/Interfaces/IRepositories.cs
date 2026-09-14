using Trippy.Backend.Data.Entities;

namespace Trippy.Backend.Interfaces;

// Repository contracts — one per aggregate. Services depend on these instead of
// AppDbContext directly, so persistence details stay behind the repository boundary.

public interface IPlaceRepository
{
    Task<IReadOnlyList<Place>> ListAsync(string? city, string? type, CancellationToken ct = default);
    Task<Place?> GetAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}

public interface IItineraryRepository
{
    Task<IReadOnlyList<Itinerary>> ListAsync(CancellationToken ct = default);
    Task<Itinerary?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Itinerary?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    void Add(Itinerary itinerary);
    void Remove(Itinerary itinerary);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface ISectionRepository
{
    Task<IReadOnlyList<ItinerarySection>> ListAsync(Guid? itineraryId, CancellationToken ct = default);
    Task<ItinerarySection?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ItinerarySection?> GetWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<int> NextSequenceAsync(Guid itineraryId, CancellationToken ct = default);
    void Add(ItinerarySection section);
    void Remove(ItinerarySection section);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IItemRepository
{
    Task<ItineraryItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ItineraryItem?> GetWithPlaceAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItineraryItem>> ListBySectionAsync(Guid sectionId, CancellationToken ct = default);
    Task<int> NextSequenceAsync(Guid sectionId, CancellationToken ct = default);
    void Add(ItineraryItem item);
    void Remove(ItineraryItem item);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IPendingActionRepository
{
    Task<PendingAction?> GetAsync(Guid id, CancellationToken ct = default);
    void Add(PendingAction action);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
