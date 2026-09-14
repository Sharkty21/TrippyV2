using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api/itineraries")]
[Tags("itineraries")]
public sealed class ItinerariesController(IItineraryService itineraries) : ControllerBase
{
    [HttpGet(Name = "list_itineraries")]
    [ProducesResponseType(typeof(IReadOnlyList<ItinerarySummary>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<ItinerarySummary>> List(CancellationToken ct)
    {
        var rows = await itineraries.ListAsync(ct);
        return rows.Select(i => i.ToSummary()).ToList();
    }

    [HttpPost(Name = "create_itinerary")]
    [ProducesResponseType(typeof(ItineraryDetail), StatusCodes.Status201Created)]
    public async Task<ActionResult<ItineraryDetail>> Create([FromBody] ItineraryCreate body, CancellationToken ct)
    {
        var created = await itineraries.CreateAsync(
            new CreateItineraryRequest(body.Name, body.Description, body.StartDate), ct);
        var dto = created.ToDetail();
        return CreatedAtAction(nameof(Get), new { itineraryId = dto.Id }, dto);
    }

    [HttpGet("{itineraryId:guid}", Name = "get_itinerary")]
    [ProducesResponseType(typeof(ItineraryDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItineraryDetail>> Get(Guid itineraryId, CancellationToken ct)
    {
        var detail = await itineraries.GetAsync(itineraryId, ct);
        return detail is null ? NotFound() : detail.ToDetail();
    }

    [HttpPatch("{itineraryId:guid}", Name = "update_itinerary")]
    [ProducesResponseType(typeof(ItineraryDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItineraryDetail>> Update(
        Guid itineraryId, [FromBody] ItineraryUpdate body, CancellationToken ct)
    {
        var updated = await itineraries.UpdateAsync(itineraryId, new UpdateItineraryRequest(
            body.Name,
            body.Description,
            body.StartDate is null ? null : DateOnly.Parse(body.StartDate)), ct);
        return updated is null ? NotFound() : updated.ToDetail();
    }

    [HttpDelete("{itineraryId:guid}", Name = "delete_itinerary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid itineraryId, CancellationToken ct)
        => await itineraries.DeleteAsync(itineraryId, ct) ? NoContent() : NotFound();
}
