namespace Trippy.Backend.Data.Entities;

public enum ActionStatus
{
    Pending,
    Approved,
    Denied,
    Executed
}

/// <summary>
/// Agent-proposed mutation waiting for human approve/deny before the executor runs it.
/// Payload shape: { "op": "create|update|delete", "entity": "itinerary|section|item", "id": "...", "data": { } }
/// </summary>
public class PendingAction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConversationId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Summary { get; set; } = string.Empty;
    public ActionStatus Status { get; set; } = ActionStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ResultJson { get; set; }
}
