using System.Text.Json;
using Trippy.Backend.Data.Entities;
using Trippy.Backend.DTOs;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services;

/// <summary>
/// Executes an approved <see cref="PendingAction"/> payload by delegating to the same
/// itinerary/section/item CRUD services used by the API, so approved agent mutations
/// and direct API mutations share one code path.
/// Extend by adding cases for new entity types — keep tool proposals + this executor in sync.
/// </summary>
public sealed class PendingActionExecutor(
    IItineraryService itineraries,
    ISectionService sections,
    IItemService items,
    ILogger<PendingActionExecutor> logger)
{
    public async Task<Dictionary<string, object?>> ExecuteAsync(PendingAction action, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(action.PayloadJson);
        var root = doc.RootElement;
        var op = root.GetProperty("op").GetString()?.ToLowerInvariant()
                 ?? throw new InvalidOperationException("Missing op");
        var entity = root.GetProperty("entity").GetString()?.ToLowerInvariant()
                     ?? throw new InvalidOperationException("Missing entity");
        var id = root.TryGetProperty("id", out var idEl) && idEl.ValueKind != JsonValueKind.Null
            ? idEl.GetString()
            : null;
        var data = root.TryGetProperty("data", out var dataEl) ? dataEl : default;

        logger.LogDebug(
            "Executing pending action. action_id={ActionId} entity={Entity} op={Op} id={Id}",
            action.Id, entity, op, id);

        return entity switch
        {
            "itinerary" => await MutateItineraryAsync(op, id, data, ct),
            "section" => await MutateSectionAsync(op, id, data, ct),
            "item" => await MutateItemAsync(op, id, data, ct),
            "plan" => await CreatePlanAsync(data, ct),
            _ => throw new InvalidOperationException($"Unsupported entity: {entity}")
        };
    }

    /// <summary>
    /// Builds a whole itinerary (3 days, auto-created) plus items per day in one approval.
    /// Payload shape: { itinerary: { name, description, start_date }, days: [ { day_index, description, items: [ { place_id, description, start_time } ] } ] }
    /// </summary>
    private async Task<Dictionary<string, object?>> CreatePlanAsync(JsonElement data, CancellationToken ct)
    {
        var itinEl = data.GetProperty("itinerary");
        var itin = await itineraries.CreateAsync(new CreateItineraryRequest(
            Name: itinEl.GetProperty("name").GetString()!,
            Description: itinEl.TryGetProperty("description", out var d) ? d.GetString() : null,
            StartDate: DateOnly.Parse(itinEl.GetProperty("start_date").GetString()!)), ct);

        var daySections = itin.Sections.OrderBy(s => s.Sequence).ToList();
        var itemCount = 0;

        if (data.TryGetProperty("days", out var daysEl) && daysEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var dayEl in daysEl.EnumerateArray())
            {
                var dayIndex = dayEl.TryGetProperty("day_index", out var diEl) ? diEl.GetInt32() : 0;
                if (dayIndex < 0 || dayIndex >= daySections.Count) continue;
                var section = daySections[dayIndex];

                if (dayEl.TryGetProperty("description", out var descEl))
                {
                    await sections.UpdateAsync(section.Id,
                        new UpdateSectionRequest(null, descEl.GetString() ?? "", null), ct);
                }

                if (!dayEl.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var itemEl in itemsEl.EnumerateArray())
                {
                    var startTime = itemEl.TryGetProperty("start_time", out var stEl) && stEl.ValueKind == JsonValueKind.String
                        ? stEl.GetString()
                        : null;
                    await items.CreateAsync(new CreateItemRequest(
                        SectionId: section.Id,
                        PlaceId: itemEl.GetProperty("place_id").GetString()!,
                        Description: itemEl.TryGetProperty("description", out var idesc) ? idesc.GetString() : null,
                        Sequence: null,
                        StartTime: string.IsNullOrWhiteSpace(startTime) ? null : TimeOnly.Parse(startTime)), ct);
                    itemCount++;
                }
            }
        }

        return new Dictionary<string, object?>
        {
            ["entity"] = "plan",
            ["id"] = itin.Id.ToString(),
            ["op"] = "create",
            ["sections_created"] = daySections.Count,
            ["items_created"] = itemCount
        };
    }

    private async Task<Dictionary<string, object?>> MutateItineraryAsync(
        string op, string? id, JsonElement data, CancellationToken ct)
    {
        if (op == "create")
        {
            var created = await itineraries.CreateAsync(new CreateItineraryRequest(
                Name: data.GetProperty("name").GetString()!,
                Description: data.TryGetProperty("description", out var d) ? d.GetString() : null,
                StartDate: DateOnly.Parse(data.GetProperty("start_date").GetString()!)), ct);
            return Result("itinerary", created.Id.ToString(), op);
        }

        var itineraryId = Guid.Parse(id!);
        if (op == "delete")
        {
            if (!await itineraries.DeleteAsync(itineraryId, ct))
                throw new InvalidOperationException("Itinerary not found");
            return Result("itinerary", id, op);
        }

        var updated = await itineraries.UpdateAsync(itineraryId, new UpdateItineraryRequest(
            Name: data.TryGetProperty("name", out var name) ? name.GetString() : null,
            Description: data.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : null,
            StartDate: data.TryGetProperty("start_date", out var sd) ? DateOnly.Parse(sd.GetString()!) : null), ct)
            ?? throw new InvalidOperationException("Itinerary not found");
        return Result("itinerary", updated.Id.ToString(), op);
    }

    private async Task<Dictionary<string, object?>> MutateSectionAsync(
        string op, string? id, JsonElement data, CancellationToken ct)
    {
        if (op == "create")
        {
            var created = await sections.CreateAsync(new CreateSectionRequest(
                ItineraryId: Guid.Parse(data.GetProperty("itinerary_id").GetString()!),
                Date: DateOnly.Parse(data.GetProperty("date").GetString()!),
                Description: data.TryGetProperty("description", out var d) ? d.GetString() : null,
                Sequence: data.TryGetProperty("sequence", out var seqEl) && seqEl.ValueKind == JsonValueKind.Number
                    ? seqEl.GetInt32()
                    : null), ct);
            return Result("section", created.Id.ToString(), op);
        }

        var sectionId = Guid.Parse(id!);
        if (op == "delete")
        {
            if (!await sections.DeleteAsync(sectionId, ct))
                throw new InvalidOperationException("Section not found");
            return Result("section", id, op);
        }

        var updated = await sections.UpdateAsync(sectionId, new UpdateSectionRequest(
            Date: data.TryGetProperty("date", out var date) ? DateOnly.Parse(date.GetString()!) : null,
            Description: data.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : null,
            Sequence: data.TryGetProperty("sequence", out var seq) ? seq.GetInt32() : null), ct)
            ?? throw new InvalidOperationException("Section not found");
        return Result("section", updated.Id.ToString(), op);
    }

    private async Task<Dictionary<string, object?>> MutateItemAsync(
        string op, string? id, JsonElement data, CancellationToken ct)
    {
        if (op == "create")
        {
            var startTimeEl = data.TryGetProperty("start_time", out var stEl) && stEl.ValueKind == JsonValueKind.String
                ? stEl.GetString()
                : null;
            var created = await items.CreateAsync(new CreateItemRequest(
                SectionId: Guid.Parse(data.GetProperty("section_id").GetString()!),
                PlaceId: data.GetProperty("place_id").GetString()!,
                Description: data.TryGetProperty("description", out var d) ? d.GetString() : null,
                Sequence: data.TryGetProperty("sequence", out var seqEl) && seqEl.ValueKind == JsonValueKind.Number
                    ? seqEl.GetInt32()
                    : null,
                StartTime: string.IsNullOrWhiteSpace(startTimeEl) ? null : TimeOnly.Parse(startTimeEl)), ct);
            return Result("item", created.Id.ToString(), op);
        }

        var itemId = Guid.Parse(id!);
        if (op == "delete")
        {
            if (!await items.DeleteAsync(itemId, ct))
                throw new InvalidOperationException("Item not found");
            return Result("item", id, op);
        }

        var updateStartTimeEl = data.TryGetProperty("start_time", out var ustEl) && ustEl.ValueKind == JsonValueKind.String
            ? ustEl.GetString()
            : null;
        var updated = await items.UpdateAsync(itemId, new UpdateItemRequest(
            PlaceId: data.TryGetProperty("place_id", out var placeId) ? placeId.GetString() : null,
            Description: data.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : null,
            Sequence: data.TryGetProperty("sequence", out var seq) ? seq.GetInt32() : null,
            StartTime: string.IsNullOrWhiteSpace(updateStartTimeEl) ? null : TimeOnly.Parse(updateStartTimeEl),
            ClearStartTime: updateStartTimeEl is not null && updateStartTimeEl.Trim().Length == 0), ct)
            ?? throw new InvalidOperationException("Item not found");
        return Result("item", updated.Id.ToString(), op);
    }

    private static Dictionary<string, object?> Result(string entity, string? id, string op) =>
        new() { ["entity"] = entity, ["id"] = id, ["op"] = op };
}
