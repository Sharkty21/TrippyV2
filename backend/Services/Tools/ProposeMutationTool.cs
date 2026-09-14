using System.Text.Json;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services.Tools;

/// <summary>
/// Never mutates itinerary tables directly — inserts a PendingAction for human approval.
/// </summary>
public sealed class ProposeMutationTool(IPendingActionRepository actions) : IAgentTool
{
    public string Name => "propose_itinerary_mutation";
    public string Description =>
        "Propose a create/update/delete on itinerary, section, item — or a full multi-day plan. " +
        "Does NOT apply the change — creates a pending action for human approval. " +
        "Itineraries are always exactly 3 days (start_date only; end_date and the 3 day sections are automatic). " +
        "Use entity='plan' with op='create' to build a whole itinerary with sections and items in ONE proposal " +
        "when the user asks you to plan a full trip — this is much better than many small item proposals. " +
        "Plan data shape: { itinerary: { name, description, start_date }, " +
        "days: [ { day_index: 0-2, description, items: [ { place_id, description, start_time (\"HH:mm\") } ] } ] }. " +
        "Item create/update data may include start_time (\"HH:mm\", 24h local time) so the UI can build a timeline. " +
        "Always mention in the summary when any included place has booking_required=true, so the human remembers to book.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            op = new { type = "string", description = "create | update | delete" },
            entity = new { type = "string", description = "itinerary | section | item | plan" },
            summary = new { type = "string", description = "Short human-readable summary of the change. Call out booking_required=true places explicitly." },
            id = new { type = "string", description = "Entity UUID for update/delete (not used for entity=plan)" },
            data = new
            {
                type = "object",
                description =
                    "Fields for create/update. Section create: itinerary_id, date, description. " +
                    "Item create/update: section_id, place_id, description, start_time (\"HH:mm\"). " +
                    "Plan create: { itinerary: { name, description, start_date }, days: [ { day_index, description, items: [ { place_id, description, start_time } ] } ] }."
            }
        },
        required = new[] { "op", "entity", "summary" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, string conversationId, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var root = doc.RootElement;
        var op = root.TryGetProperty("op", out var opEl) ? opEl.GetString()?.ToLowerInvariant() : null;
        var entity = root.TryGetProperty("entity", out var entEl) ? entEl.GetString()?.ToLowerInvariant() : null;
        var summary = root.TryGetProperty("summary", out var sumEl) ? sumEl.GetString() ?? "" : "";
        var id = root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null
            ? idEl.GetString()
            : null;
        var hasData = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object;

        if (op is not ("create" or "update" or "delete"))
            return JsonSerializer.Serialize(new { error = "op must be create|update|delete" });
        if (entity is not ("itinerary" or "section" or "item" or "plan"))
            return JsonSerializer.Serialize(new { error = "entity must be itinerary|section|item|plan" });
        if (entity == "plan" && op != "create")
            return JsonSerializer.Serialize(new { error = "entity=plan only supports op=create" });
        if (op is ("update" or "delete") && string.IsNullOrWhiteSpace(id))
            return JsonSerializer.Serialize(new { error = "id is required for update/delete" });
        if (op is ("create" or "update") && !hasData)
            return JsonSerializer.Serialize(new { error = "data is required for create/update" });

        var payload = new Dictionary<string, object?>
        {
            ["op"] = op,
            ["entity"] = entity,
            ["id"] = id,
            ["data"] = hasData
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(dataEl.GetRawText())
                : new Dictionary<string, object?>()
        };

        var action = new PendingAction
        {
            ConversationId = conversationId,
            ToolName = Name,
            PayloadJson = JsonSerializer.Serialize(payload),
            Summary = summary,
            Status = ActionStatus.Pending
        };
        actions.Add(action);
        await actions.SaveChangesAsync(ct);

        return JsonSerializer.Serialize(new
        {
            status = "pending_approval",
            action_id = action.Id.ToString(),
            summary,
            command = payload,
            message = "Waiting for human approval. Do not claim the change was applied."
        });
    }
}
