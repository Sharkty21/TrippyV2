using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Controllers;

[ApiController]
[Route("api")]
[Tags("chat")]
public sealed class ChatController(IChatAgent agent, IPendingActionService actions) : ControllerBase
{
    /// <summary>
    /// Streams SSE events: conversation, token, tool, pending_action, error, done.
    /// Marked as produces application/json for OpenAPI/Orval; the real content-type is text/event-stream.
    /// </summary>
    [HttpPost("chat", Name = "chat")]
    [Produces("text/event-stream")]
    public async Task Chat([FromBody] ChatRequest body, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";
        Response.ContentType = "text/event-stream";
        HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()
            ?.DisableBuffering();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        await foreach (var evt in agent.StreamAsync(body.Message, body.ConversationId, ct))
        {
            var data = JsonSerializer.Serialize(evt.Data, jsonOptions);
            await Response.WriteAsync($"event: {evt.Event}\ndata: {data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    [HttpGet("actions/{actionId:guid}", Name = "get_action")]
    [ProducesResponseType(typeof(PendingActionRead), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PendingActionRead>> GetAction(Guid actionId, CancellationToken ct)
    {
        var action = await actions.GetAsync(actionId, ct);
        return action is null ? NotFound() : action.ToDto();
    }

    [HttpPost("actions/{actionId:guid}/approve", Name = "approve_action")]
    [ProducesResponseType(typeof(PendingActionRead), StatusCodes.Status200OK)]
    public async Task<ActionResult<PendingActionRead>> Approve(Guid actionId, CancellationToken ct)
    {
        try
        {
            var action = await actions.ApproveAsync(actionId, ct);
            return action is null ? NotFound() : action.ToDto();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("actions/{actionId:guid}/deny", Name = "deny_action")]
    [ProducesResponseType(typeof(PendingActionRead), StatusCodes.Status200OK)]
    public async Task<ActionResult<PendingActionRead>> Deny(Guid actionId, CancellationToken ct)
    {
        try
        {
            var action = await actions.DenyAsync(actionId, ct);
            return action is null ? NotFound() : action.ToDto();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
