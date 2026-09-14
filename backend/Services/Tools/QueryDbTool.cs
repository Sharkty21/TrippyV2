using System.Collections;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services.Tools;

/// <summary>
/// Read-only DB lookup for the agent. Filters are simple equality / contains.
/// Add new tables by extending the switch — keep responses compact for the model context.
/// Queries AppDbContext directly (read-only reporting across aggregates, not a single
/// repository's responsibility).
/// </summary>
public sealed class QueryDbTool(AppDbContext db, ILogger<QueryDbTool> logger) : IAgentTool
{
    public string Name => "query_db";
    public string Description =>
        "Read/query places, itineraries, sections, or items from the database.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            table = new { type = "string", description = "One of: places, itineraries, sections, items" },
            filters = new { type = "object", description = "Optional equality filters, e.g. {\"city\":\"Rome\"}" },
            limit = new { type = "integer", description = "Max rows (cap 50)", @default = 20 }
        },
        required = new[] { "table" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, string conversationId, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var root = doc.RootElement;
        var table = root.GetProperty("table").GetString()?.ToLowerInvariant() ?? "";
        var limit = root.TryGetProperty("limit", out var lim) ? Math.Clamp(lim.GetInt32(), 1, 50) : 20;
        var filters = root.TryGetProperty("filters", out var f) && f.ValueKind == JsonValueKind.Object
            ? f
            : default;

        logger.LogInformation(
            "query_db called. conversation_id={ConversationId} table={Table} filters={Filters} limit={Limit}",
            conversationId, table, filters.ValueKind == JsonValueKind.Object ? filters.GetRawText() : "{}", limit);

        object rows = table switch
        {
            "places" => await QueryPlacesAsync(filters, limit, ct),
            "itineraries" => await QueryItinerariesAsync(filters, limit, ct),
            "sections" => await QuerySectionsAsync(filters, limit, ct),
            "items" => await QueryItemsAsync(filters, limit, ct),
            _ => new { error = $"Unknown table '{table}'. Use places|itineraries|sections|items" }
        };

        if (rows is ICollection { Count: 0 })
        {
            logger.LogWarning(
                "query_db returned zero rows. conversation_id={ConversationId} table={Table} filters={Filters}",
                conversationId, table, filters.ValueKind == JsonValueKind.Object ? filters.GetRawText() : "{}");
        }

        return JsonSerializer.Serialize(rows);
    }

    private async Task<object> QueryPlacesAsync(JsonElement filters, int limit, CancellationToken ct)
    {
        var q = db.Places.AsNoTracking().AsQueryable();
        if (filters.ValueKind == JsonValueKind.Object)
        {
            if (TryGet(filters, "city", out var city)) q = q.Where(p => p.City == city);
            if (TryGet(filters, "type", out var type)) q = q.Where(p => p.Type == type);
            if (TryGet(filters, "id", out var id)) q = q.Where(p => p.Id == id);
            if (TryGet(filters, "name_contains", out var name))
                q = q.Where(p => EF.Functions.Like(p.Name, $"%{name}%"));
        }

        return await q.Take(limit).Select(p => new
        {
            p.Id,
            p.Name,
            p.Type,
            p.City,
            p.Region,
            p.Neighborhood,
            description = p.Description.Length > 200 ? p.Description.Substring(0, 200) : p.Description,
            p.Latitude,
            p.Longitude,
            p.Hours,
            p.DurationMinutes,
            p.PriceRange,
            p.Rating,
            p.Tags,
            p.SeasonalNotes,
            p.BookingRequired
        }).ToListAsync(ct);
    }

    private async Task<object> QueryItinerariesAsync(JsonElement filters, int limit, CancellationToken ct)
    {
        var q = db.Itineraries.AsNoTracking().AsQueryable();
        if (filters.ValueKind == JsonValueKind.Object && TryGet(filters, "id", out var id))
            q = q.Where(i => i.Id == Guid.Parse(id));

        return await q.Take(limit).Select(i => new
        {
            id = i.Id.ToString(),
            i.Name,
            i.Description,
            start_date = i.StartDate.ToString("yyyy-MM-dd"),
            end_date = i.EndDate.ToString("yyyy-MM-dd")
        }).ToListAsync(ct);
    }

    private async Task<object> QuerySectionsAsync(JsonElement filters, int limit, CancellationToken ct)
    {
        var q = db.Sections.AsNoTracking().AsQueryable();
        if (filters.ValueKind == JsonValueKind.Object)
        {
            if (TryGet(filters, "itinerary_id", out var itinId))
                q = q.Where(s => s.ItineraryId == Guid.Parse(itinId));
            if (TryGet(filters, "id", out var id))
                q = q.Where(s => s.Id == Guid.Parse(id));
        }

        return await q.Take(limit).Select(s => new
        {
            id = s.Id.ToString(),
            itinerary_id = s.ItineraryId.ToString(),
            date = s.Date.ToString("yyyy-MM-dd"),
            s.Description,
            s.Sequence
        }).ToListAsync(ct);
    }

    private async Task<object> QueryItemsAsync(JsonElement filters, int limit, CancellationToken ct)
    {
        var q = db.Items.AsNoTracking().AsQueryable();
        if (filters.ValueKind == JsonValueKind.Object)
        {
            if (TryGet(filters, "section_id", out var sectionId))
                q = q.Where(i => i.SectionId == Guid.Parse(sectionId));
            if (TryGet(filters, "id", out var id))
                q = q.Where(i => i.Id == Guid.Parse(id));
        }

        return await q.Take(limit).Select(i => new
        {
            id = i.Id.ToString(),
            section_id = i.SectionId.ToString(),
            place_id = i.PlaceId,
            i.Description,
            i.Sequence,
            start_time = i.StartTime.HasValue ? i.StartTime.Value.ToString("HH:mm") : null
        }).ToListAsync(ct);
    }

    private static bool TryGet(JsonElement filters, string key, out string value)
    {
        value = "";
        if (!filters.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString() ?? "";
        return !string.IsNullOrEmpty(value);
    }
}
