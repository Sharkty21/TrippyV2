using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api/places")]
[Tags("places")]
public sealed class PlacesController(IPlaceService places) : ControllerBase
{
    [HttpGet(Name = "list_places")]
    [ProducesResponseType(typeof(IReadOnlyList<PlaceRead>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PlaceRead>> List([FromQuery] string? city, [FromQuery] string? type, CancellationToken ct)
    {
        var rows = await places.ListAsync(city, type, ct);
        return rows.Select(p => p.ToDto()).ToList();
    }

    [HttpGet("{placeId}", Name = "get_place")]
    [ProducesResponseType(typeof(PlaceRead), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlaceRead>> Get(string placeId, CancellationToken ct)
    {
        var place = await places.GetAsync(placeId, ct);
        return place is null ? NotFound() : place.ToDto();
    }
}
