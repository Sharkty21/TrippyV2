using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api/sections")]
[Tags("sections")]
public sealed class SectionsController(ISectionService sections) : ControllerBase
{
    [HttpGet(Name = "list_sections")]
    [ProducesResponseType(typeof(IReadOnlyList<SectionRead>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<SectionRead>> List([FromQuery] Guid? itinerary_id, CancellationToken ct)
    {
        var rows = await sections.ListAsync(itinerary_id, ct);
        return rows.Select(s => s.ToDto()).ToList();
    }

    [HttpPost(Name = "create_section")]
    [ProducesResponseType(typeof(SectionRead), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SectionRead>> Create([FromBody] SectionCreate body, CancellationToken ct)
    {
        try
        {
            var created = await sections.CreateAsync(
                new CreateSectionRequest(body.ItineraryId, body.Date, body.Description, body.Sequence), ct);
            return StatusCode(StatusCodes.Status201Created, created.ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPatch("{sectionId:guid}", Name = "update_section")]
    [ProducesResponseType(typeof(SectionRead), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionRead>> Update(
        Guid sectionId, [FromBody] SectionUpdate body, CancellationToken ct)
    {
        var updated = await sections.UpdateAsync(sectionId, new UpdateSectionRequest(
            body.Date is null ? null : DateOnly.Parse(body.Date),
            body.Description,
            body.Sequence), ct);
        return updated is null ? NotFound() : updated.ToDto();
    }

    [HttpDelete("{sectionId:guid}", Name = "delete_section")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid sectionId, CancellationToken ct)
        => await sections.DeleteAsync(sectionId, ct) ? NoContent() : NotFound();
}
