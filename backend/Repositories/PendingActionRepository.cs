using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Repositories;

public sealed class PendingActionRepository(AppDbContext db) : IPendingActionRepository
{
    public async Task<PendingAction?> GetAsync(Guid id, CancellationToken ct = default) =>
        await db.PendingActions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Add(PendingAction action) => db.PendingActions.Add(action);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
