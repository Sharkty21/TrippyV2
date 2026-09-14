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
    IItemRepository items,
    ISectionRepository sections,
    ILogger<ProposeMutationTool> logger) : IAgentTool
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
        "place_id (item create, and every plan item) MUST be the exact 'id' value returned by query_db " +
        "(table='places') — never invent, guess, or reuse a placeholder id like 'place_001'; a mismatched " +
        "place_id is rejected. Always call query_db to find the real id for the place you mean before proposing. " +
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
            id = new
            {
                type = "string",
                description = "Entity UUID for update/delete (not used for entity=plan). This is the ONLY " +
                    "identifier that belongs at the top level — every other id (section_id, place_id, " +
                    "itinerary_id, etc.) belongs INSIDE `data`, never here and never as a sibling of `data`."
            },
            data = new
            {
                type = "object",
                description =
                    "ALL other fields for create/update go INSIDE this object — never at the top level " +
                    "alongside `op`/`entity`/`id`. Section create: { itinerary_id, date, description }. " +
                    "Item create/update: { section_id, place_id, description, start_time }. " +
                    "Plan create: { itinerary: { name, description, start_date }, days: [ { day_index, " +
                    "description, items: [ { place_id, description, start_time } ] } ] }.",
                properties = new
                {
                    section_id = new
                    {
                        type = "string",
                        description = "Item create (REQUIRED, must be nested here inside data — not at the " +
                            "top level): the target day's section id. Must be a real existing section id " +
                            "from the ACTIVE ITINERARY CONTEXT's section list or a query_db table=\"sections\" " +
                            "result; missing/unknown section_id is rejected."
                    },
                    place_id = new
                    {
                        type = "string",
                        description = "Item create, and every plan item (REQUIRED): must be the exact 'id' " +
                            "returned by query_db (table=\"places\") for the intended place — never a made-up/" +
                            "placeholder id like \"place_001\"; an unrecognized place_id is rejected."
                    },
                    description = new
                    {
                        type = "string",
                        description = "Item create/update, and every plan item (REQUIRED on create): short " +
                            "concrete advice for that stop — what to order/do/bring/skip, or a timing tip — " +
                            "not just the place name."
                    },
                    start_time = new
                    {
                        type = "string",
                        description = "Item create/update, and plan items: \"HH:mm\" 24h local time."
                    },
                    itinerary_id = new { type = "string", description = "Section create (REQUIRED): the parent itinerary's id." },
                    date = new { type = "string", description = "Section create: \"yyyy-MM-dd\", must be within the itinerary's 3-day range." }
                }
            }
        },
        required = new[] { "op", "entity", "summary" }
    };

    /// <summary>
    /// Fields that only ever belong nested inside `data`, never at the top level. Models
    /// occasionally place one of these as a sibling of `data` (probably by analogy with the
    /// top-level `id` field) — instead of rejecting that shape outright and forcing several
    /// identical, uncorrected retries, we fold it into `data` here and just log that we did so.
    /// </summary>
    private static readonly string[] MisplaceableDataKeys =
        ["section_id", "place_id", "description", "start_time", "itinerary_id", "date"];

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
        var hasData = root.TryGetProperty("data", out var rawDataEl) && rawDataEl.ValueKind == JsonValueKind.Object;

        // Self-heal: fold any of MisplaceableDataKeys found at the top level (instead of inside
        // `data`, where they belong) into `data`, so a misplaced field doesn't cause a rejection
        // the model can't productively act on.
        JsonDocument? mergedDoc = null;
        var dataEl = rawDataEl;
        var foldedKeys = new List<string>();
        foreach (var key in MisplaceableDataKeys)
        {
            if (root.TryGetProperty(key, out var rootVal) && rootVal.ValueKind != JsonValueKind.Null)
                foldedKeys.Add(key);
        }
        if (foldedKeys.Count > 0)
        {
            var merged = new Dictionary<string, JsonElement>();
            if (hasData)
            {
                foreach (var prop in rawDataEl.EnumerateObject())
                    merged[prop.Name] = prop.Value.Clone();
            }
            foreach (var key in foldedKeys)
            {
                if (!merged.ContainsKey(key) && root.TryGetProperty(key, out var rootVal))
                    merged[key] = rootVal.Clone();
            }
            mergedDoc = JsonDocument.Parse(JsonSerializer.Serialize(merged));
            dataEl = mergedDoc.RootElement;
            hasData = true;

            logger.LogWarning(
                "propose_itinerary_mutation: folded misplaced top-level field(s) {Keys} into `data`. " +
                "conversation_id={ConversationId} op={Op} entity={Entity}",
                string.Join(",", foldedKeys), conversationId, op, entity);
        }

        try
        {
            return await ExecuteCoreAsync(op, entity, summary, id, hasData, dataEl, conversationId, ct);
        }
        finally
        {
            mergedDoc?.Dispose();
        }
    }

    private async Task<string> ExecuteCoreAsync(
        string? op, string? entity, string summary, string? id, bool hasData, JsonElement dataEl,
        string conversationId, CancellationToken ct)
    {
        logger.LogInformation(
            "propose_itinerary_mutation called. conversation_id={ConversationId} op={Op} entity={Entity} id={Id} has_data={HasData}",
            conversationId, op, entity, id, hasData);

        string? RejectWith(string reason)
        {
            logger.LogWarning(
                "propose_itinerary_mutation rejected. conversation_id={ConversationId} op={Op} entity={Entity} id={Id} reason={Reason}",
                conversationId, op, entity, id, reason);
            return reason;
        }

        if (op is not ("create" or "update" or "delete"))
            return JsonSerializer.Serialize(new { error = RejectWith("op must be create|update|delete") });
        if (entity is not ("itinerary" or "section" or "item" or "plan"))
            return JsonSerializer.Serialize(new { error = RejectWith("entity must be itinerary|section|item|plan") });
        if (entity == "plan" && op != "create")
            return JsonSerializer.Serialize(new { error = RejectWith("entity=plan only supports op=create") });
        if (op is ("update" or "delete") && string.IsNullOrWhiteSpace(id))
            return JsonSerializer.Serialize(new { error = RejectWith("id is required for update/delete") });
        if (op is ("create" or "update") && !hasData)
            return JsonSerializer.Serialize(new { error = RejectWith("data is required for create/update") });

        var placeIdError = entity switch
        {
            "item" => await ValidateItemPlaceIdAsync(op!, hasData ? dataEl : default, ct),
            "plan" => await ValidatePlanPlaceIdsAsync(hasData ? dataEl : default, ct),
            _ => null
        };
        if (placeIdError is not null)
            return JsonSerializer.Serialize(new { error = RejectWith(placeIdError) });

        if (entity == "item")
        {
            var sectionIdError = await ValidateItemSectionIdAsync(op!, hasData ? dataEl : default, ct);
            if (sectionIdError is not null)
                return JsonSerializer.Serialize(new { error = RejectWith(sectionIdError) });
        }

        var descriptionError = entity switch
        {
            "item" => await ValidateItemDescriptionAsync(op!, id, hasData ? dataEl : default, ct),
            "plan" => await ValidatePlanDescriptionAsync(hasData ? dataEl : default, ct),
            _ => null
        };
        if (descriptionError is not null)
            return JsonSerializer.Serialize(new { error = RejectWith(descriptionError) });

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

        logger.LogInformation(
            "Pending action queued. conversation_id={ConversationId} action_id={ActionId} op={Op} entity={Entity}",
            conversationId, action.Id, op, entity);

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
    /// For entity=item: on create, place_id must reference a real row in the places table.
    /// This is the hard guard against the model fabricating a placeholder id (e.g. "place_001")
    /// instead of using an id it actually looked up via query_db.
    /// </summary>
    private async Task<string?> ValidateItemPlaceIdAsync(string op, JsonElement data, CancellationToken ct)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;
        if (!data.TryGetProperty("place_id", out var placeIdEl) || placeIdEl.ValueKind != JsonValueKind.String)
        {
            // place_id is required on create; absence on update just means "leave unchanged".
            return op == "create"
                ? "data.place_id is required and must be a real place id from query_db (table=\"places\")."
                : null;
        }

        var placeId = placeIdEl.GetString();
        if (string.IsNullOrWhiteSpace(placeId) || !await places.ExistsAsync(placeId, ct))
        {
            return $"place_id '{placeId}' does not exist in the places table — you must not invent or guess a " +
                   "place id (e.g. \"place_001\"). Call query_db with table=\"places\" first and use the exact " +
                   "'id' value returned for the place you want, then retry with that id.";
        }

        return null;
    }

    /// <summary>
    /// For entity=item: on create, section_id is required and must reference a real, existing
    /// section (a valid GUID). Without this guard, an item create missing/mis-typing section_id
    /// silently creates an unusable pending action that then throws when a human approves it.
    /// </summary>
    private async Task<string?> ValidateItemSectionIdAsync(string op, JsonElement data, CancellationToken ct)
    {
        if (op != "create") return null; // update/delete target an existing item's id; section_id isn't required.
        if (data.ValueKind != JsonValueKind.Object) return null;

        if (!data.TryGetProperty("section_id", out var sectionIdEl) || sectionIdEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(sectionIdEl.GetString()))
        {
            return "data.section_id is required for item create — use the target day's section_id (see the " +
                   "ACTIVE ITINERARY CONTEXT's section list, or call query_db with table=\"sections\").";
        }

        var raw = sectionIdEl.GetString()!;
        if (!Guid.TryParse(raw, out var sectionGuid))
        {
            return $"data.section_id '{raw}' is not a valid id — use the exact section_id from the ACTIVE " +
                   "ITINERARY CONTEXT or a query_db table=\"sections\" result.";
        }

        if (await sections.GetAsync(sectionGuid, ct) is null)
        {
            return $"data.section_id '{raw}' does not reference an existing section — use the exact section_id " +
                   "from the ACTIVE ITINERARY CONTEXT or a query_db table=\"sections\" result.";
        }

        return null;
    }

    /// <summary>
    /// For entity=plan: checks every items[] entry's place_id, since every plan item is a create.
    /// </summary>
    private async Task<string?> ValidatePlanPlaceIdsAsync(JsonElement data, CancellationToken ct)
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
                {
                    return $"day_index {dayIndex}: data.place_id is required for every plan item and must be a " +
                           "real place id from query_db (table=\"places\").";
                }

                var placeId = placeIdEl.GetString();
                if (string.IsNullOrWhiteSpace(placeId) || !await places.ExistsAsync(placeId, ct))
                {
                    return $"day_index {dayIndex}: place_id '{placeId}' does not exist in the places table — you " +
                           "must not invent or guess a place id (e.g. \"place_001\"). Call query_db with " +
                           "table=\"places\" first and use the exact 'id' value returned, then retry.";
                }
            }
        }

        return null;
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
