using System.Text.Json;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

/// <summary>
/// Approve/deny lifecycle for agent-proposed pending actions. The actual mutation is
/// delegated to <see cref="PendingActionExecutor"/>, which runs the existing itinerary
/// CRUD services — this class only owns status transitions and persistence of the result.
/// </summary>
public sealed class PendingActionService(
    IPendingActionRepository actions,
    PendingActionExecutor executor,
    ILogger<PendingActionService> logger) : IPendingActionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public Task<PendingAction?> GetAsync(Guid id, CancellationToken ct = default) =>
        actions.GetAsync(id, ct);

    public async Task<PendingAction?> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var action = await actions.GetAsync(id, ct);
        if (action is null)
        {
            logger.LogWarning("Approve requested for unknown action_id={ActionId}", id);
            return null;
        }
        if (action.Status != ActionStatus.Pending)
        {
            logger.LogWarning(
                "Approve rejected: action_id={ActionId} is already {Status}", id, action.Status);
            throw new InvalidOperationException($"Action is {action.Status}");
        }

        logger.LogInformation(
            "Approving action_id={ActionId} conversation_id={ConversationId} payload={Payload}",
            id, action.ConversationId, action.PayloadJson);

        try
        {
            var result = await executor.ExecuteAsync(action, ct);
            action.Status = ActionStatus.Executed;
            action.ResultJson = JsonSerializer.Serialize(result, JsonOptions);
            await actions.SaveChangesAsync(ct);
            logger.LogInformation(
                "Action approved and executed. action_id={ActionId} result={Result}",
                id, action.ResultJson);
            return action;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException or InvalidOperationException or FormatException)
        {
            // Leave the action pending so the user can deny/retry instead of crashing the request.
            logger.LogError(ex,
                "Action execution failed, left pending for retry/deny. action_id={ActionId} conversation_id={ConversationId} payload={Payload}",
                id, action.ConversationId, action.PayloadJson);
            action.ResultJson = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
            await actions.SaveChangesAsync(ct);
            throw new InvalidOperationException($"Could not apply action: {ex.Message}");
        }
    }

    public async Task<PendingAction?> DenyAsync(Guid id, CancellationToken ct = default)
    {
        var action = await actions.GetAsync(id, ct);
        if (action is null)
        {
            logger.LogWarning("Deny requested for unknown action_id={ActionId}", id);
            return null;
        }
        if (action.Status != ActionStatus.Pending)
        {
            logger.LogWarning("Deny rejected: action_id={ActionId} is already {Status}", id, action.Status);
            throw new InvalidOperationException($"Action is {action.Status}");
        }

        action.Status = ActionStatus.Denied;
        await actions.SaveChangesAsync(ct);
        logger.LogInformation("Action denied. action_id={ActionId} conversation_id={ConversationId}", id, action.ConversationId);
        return action;
    }
}
