using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

public sealed class PlaceService(IPlaceRepository places) : IPlaceService
{
    public Task<IReadOnlyList<Place>> ListAsync(string? city, string? type, CancellationToken ct = default) =>
        places.ListAsync(city, type, ct);

    public Task<Place?> GetAsync(string id, CancellationToken ct = default) =>
        places.GetAsync(id, ct);
}
