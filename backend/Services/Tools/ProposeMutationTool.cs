using System.Text.Json;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services.Tools;

/// <summary>
/// Never mutates itinerary tables directly — inserts a PendingAction for human approval.
/// Item descriptions are gate-kept (see <see cref="ValidateItemDescriptionAsync"/>) so they always
/// carry real advice. Hours/seasonal-notes availability is intentionally NOT hard-enforced here —
/// checking that data is a strongly-worded prompt instruction instead (see ChatAgentService's system
/// prompt), because requiring an extra structured field on every item made the model slow/prone to
/// getting stuck retrying tool calls.
/// </summary>
public sealed class ProposeMutationTool(
    IPendingActionRepository actions,
    IPlaceRepository places,
    IItemRepository items) : IAgentTool
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
        "Strongly prefer calling query_db on each place first and checking its `hours` AND `seasonal_notes` " +
        "against the exact scheduled day-of-week, time-of-day, and time-of-year before including it — call out " +
        "anything that might be closed or seasonal in the summary. This is not a hard requirement of this tool " +
        "(don't get stuck retrying over it), but skipping it risks proposing something that's actually closed. " +
        "Every item's `description` must be short, concrete, helpful advice for that stop — not a generic " +
        "restatement of the place name (rejected if missing/too generic). For restaurants/cafes/bars, name a " +
        "dish/drink/specialty to try; for museums/sights, note what not to miss or how to skip lines; for " +
        "activities, note what to bring or book. Fold in timing tips from hours/seasonal_notes when relevant. " +
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
                    "Plan create: { itinerary: { name, description, start_date }, days: [ { day_index, description, " +
                    "items: [ { place_id, description, start_time } ] } ] }. " +
                    "Item description (REQUIRED, item and plan items): short concrete advice for that stop — " +
                    "what to order/do/bring/skip, or a timing tip — not just the place name."
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

        var descriptionError = entity switch
        {
            "item" => await ValidateItemDescriptionAsync(op!, id, hasData ? dataEl : default, ct),
            "plan" => await ValidatePlanDescriptionAsync(hasData ? dataEl : default, ct),
            _ => null
        };
        if (descriptionError is not null)
            return JsonSerializer.Serialize(new { error = descriptionError });

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

    /// <summary>
    /// For entity=item: description is required on create, and re-validated on update whenever it's
    /// being (re)set. Rejects missing/trivial/place-name-only descriptions.
    /// </summary>
    private async Task<string?> ValidateItemDescriptionAsync(string op, string? id, JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;

        var hasDescription = data.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String;
        var hasPlaceId = data.TryGetProperty("place_id", out var placeIdEl) && placeIdEl.ValueKind == JsonValueKind.String;

        if (op == "create")
        {
            if (!hasPlaceId) return null; // will fail create validation elsewhere
            return await CheckDescriptionAsync(hasDescription ? descEl.GetString() : null, placeIdEl.GetString(), "this item", ct);
        }

        // update: only re-check when description is actually being set.
        if (!hasDescription) return null;

        string? placeId = null;
        if (hasPlaceId)
        {
            placeId = placeIdEl.GetString();
        }
        else if (Guid.TryParse(id, out var itemGuid))
        {
            var existing = await items.GetAsync(itemGuid, ct);
            placeId = existing?.PlaceId;
        }

        return await CheckDescriptionAsync(descEl.GetString(), placeId, "this item", ct);
    }

    /// <summary>
    /// For entity=plan: checks every items[] entry's description, since every plan item is a create.
    /// </summary>
    private async Task<string?> ValidatePlanDescriptionAsync(JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;
        if (!data.TryGetProperty("days", out var daysEl) || daysEl.ValueKind != JsonValueKind.Array) return null;

        foreach (var dayEl in daysEl.EnumerateArray())
        {
            var dayIndex = dayEl.TryGetProperty("day_index", out var diEl) && diEl.ValueKind == JsonValueKind.Number
                ? diEl.GetInt32()
                : 0;

            if (!dayEl.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var itemEl in itemsEl.EnumerateArray())
            {
                if (!itemEl.TryGetProperty("place_id", out var placeIdEl) || placeIdEl.ValueKind != JsonValueKind.String)
                    continue; // will fail plan create validation elsewhere
                var description = itemEl.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                    ? descEl.GetString()
                    : null;

                var error = await CheckDescriptionAsync(description, placeIdEl.GetString(), $"day_index {dayIndex}", ct);
                if (error is not null) return error;
            }
        }

        return null;
    }

    private const int MinDescriptionLength = 8;

    /// <summary>
    /// Rejects a missing/trivial description, or one that just restates the place's name, so the
    /// agent is forced to give real advice (what to order/do/bring, or a timing tip).
    /// </summary>
    private async Task<string?> CheckDescriptionAsync(string? description, string? placeId, string itemLabel, CancellationToken ct)
    {
        var place = placeId is null ? null : await places.GetAsync(placeId, ct);
        var placeRef = place is not null ? $" — place '{place.Name}' ({place.Id})" : "";

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length < MinDescriptionLength)
        {
            return $"description is required for {itemLabel}{placeRef} and must be short, concrete, helpful " +
                   "advice (what to order/do/bring, or a timing tip) — not empty or a couple of words. " +
                   (place is not null
                       ? $"Use its type/tags/description (and hours \"{place.Hours ?? "none"}\"/seasonal_notes " +
                         $"\"{place.SeasonalNotes ?? "none"}\") for hints."
                       : "Call query_db on this place first for hints.");
        }

        if (place is not null && string.Equals(description.Trim(), place.Name.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return $"description for {itemLabel}{placeRef} just restates the place name — replace it with " +
                   "concrete advice: what to order/do/bring, or a timing tip.";
        }

        return null;
    }
}
