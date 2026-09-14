using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Repositories;

public sealed class ItineraryRepository(AppDbContext db) : IItineraryRepository
{
    public async Task<IReadOnlyList<Itinerary>> ListAsync(CancellationToken ct = default) =>
        await db.Itineraries.AsNoTracking()
            .OrderBy(i => i.StartDate)
            .ToListAsync(ct);

    public async Task<Itinerary?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Itineraries.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Itinerary?> GetDetailAsync(Guid id, CancellationToken ct = default) =>
        await db.Itineraries.AsNoTracking()
            .Include(i => i.Sections.OrderBy(s => s.Sequence))
            .ThenInclude(s => s.Items.OrderBy(x => x.Sequence))
            .ThenInclude(item => item.Place)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        db.Itineraries.AnyAsync(i => i.Id == id, ct);

    public void Add(Itinerary itinerary) => db.Itineraries.Add(itinerary);

    public void Remove(Itinerary itinerary) => db.Itineraries.Remove(itinerary);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
