using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Repositories;

public sealed class SectionRepository(AppDbContext db) : ISectionRepository
{
    public async Task<IReadOnlyList<ItinerarySection>> ListAsync(Guid? itineraryId, CancellationToken ct = default)
    {
        var q = db.Sections.AsNoTracking()
            .Include(s => s.Items.OrderBy(i => i.Sequence))
            .ThenInclude(i => i.Place)
            .AsQueryable();
        if (itineraryId is not null)
            q = q.Where(s => s.ItineraryId == itineraryId);
        return await q.OrderBy(s => s.Sequence).ToListAsync(ct);
    }

    public async Task<ItinerarySection?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.Sections.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<ItinerarySection?> GetWithItemsAsync(Guid id, CancellationToken ct = default) =>
        await db.Sections.AsNoTracking()
            .Include(s => s.Items.OrderBy(i => i.Sequence))
            .ThenInclude(i => i.Place)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<int> NextSequenceAsync(Guid itineraryId, CancellationToken ct = default)
    {
        var max = await db.Sections.Where(s => s.ItineraryId == itineraryId)
            .Select(s => (int?)s.Sequence).MaxAsync(ct);
        return (max ?? -1) + 1;
    }

    public void Add(ItinerarySection section) => db.Sections.Add(section);

    public void Remove(ItinerarySection section) => db.Sections.Remove(section);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
