using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api/items")]
[Tags("items")]
public sealed class ItemsController(IItemService items) : ControllerBase
{
    [HttpPost(Name = "create_item")]
    [ProducesResponseType(typeof(ItemRead), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemRead>> Create([FromBody] ItemCreate body, CancellationToken ct)
    {
        try
        {
            var created = await items.CreateAsync(
                new CreateItemRequest(body.SectionId, body.PlaceId, body.Description, body.Sequence,
                    string.IsNullOrWhiteSpace(body.StartTime) ? null : TimeOnly.Parse(body.StartTime)), ct);
            return StatusCode(StatusCodes.Status201Created, created.ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpPatch("{itemId:guid}", Name = "update_item")]
    [ProducesResponseType(typeof(ItemRead), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemRead>> Update(Guid itemId, [FromBody] ItemUpdate body, CancellationToken ct)
    {
        try
        {
            var updated = await items.UpdateAsync(itemId,
                new UpdateItemRequest(
                    body.PlaceId,
                    body.Description,
                    body.Sequence,
                    string.IsNullOrWhiteSpace(body.StartTime) ? null : TimeOnly.Parse(body.StartTime),
                    ClearStartTime: body.StartTime is not null && body.StartTime.Trim().Length == 0), ct);
            return updated is null ? NotFound() : updated.ToDto();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpDelete("{itemId:guid}", Name = "delete_item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid itemId, CancellationToken ct)
        => await items.DeleteAsync(itemId, ct) ? NoContent() : NotFound();

    [HttpPut("reorder/{sectionId:guid}", Name = "reorder_items")]
    [ProducesResponseType(typeof(IReadOnlyList<ItemRead>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ItemRead>>> Reorder(
        Guid sectionId, [FromBody] ItemReorderRequest body, CancellationToken ct)
    {
        try
        {
            var reordered = await items.ReorderAsync(sectionId, body.ItemIds, ct);
            return Ok(reordered.Select(i => i.ToDto()).ToList());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
