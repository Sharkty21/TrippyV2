using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Repositories;

public sealed class ItemRepository(AppDbContext db) : IItemRepository
{
    public async Task<ItineraryItem?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<ItineraryItem?> GetWithPlaceAsync(Guid id, CancellationToken ct = default) =>
        await db.Items.AsNoTracking().Include(i => i.Place).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<IReadOnlyList<ItineraryItem>> ListBySectionAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.Items.AsNoTracking()
            .Include(i => i.Place)
            .Where(i => i.SectionId == sectionId)
            .OrderBy(i => i.Sequence)
            .ToListAsync(ct);

    public async Task<int> NextSequenceAsync(Guid sectionId, CancellationToken ct = default)
    {
        var max = await db.Items.Where(i => i.SectionId == sectionId)
            .Select(i => (int?)i.Sequence).MaxAsync(ct);
        return (max ?? -1) + 1;
    }

    public void Add(ItineraryItem item) => db.Items.Add(item);

    public void Remove(ItineraryItem item) => db.Items.Remove(item);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
