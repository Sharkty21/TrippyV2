using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Repositories;

public sealed class PlaceRepository(AppDbContext db) : IPlaceRepository
{
    public async Task<IReadOnlyList<Place>> ListAsync(string? city, string? type, CancellationToken ct = default)
    {
        var q = db.Places.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(city))
            q = q.Where(p => p.City == city);
        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(p => p.Type == type);
        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<Place?> GetAsync(string id, CancellationToken ct = default) =>
        await db.Places.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsAsync(string id, CancellationToken ct = default) =>
        db.Places.AnyAsync(p => p.Id == id, ct);
}
