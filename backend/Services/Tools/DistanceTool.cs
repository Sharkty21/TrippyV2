using System.Text.Json;
using Trippy.Backend.Data;
using Trippy.Backend.Interfaces;

namespace Trippy.Backend.Services.Tools;

/// <summary>
/// Read-only helper so the agent can reason about how far apart two places are before
/// scheduling them back-to-back. Uses haversine distance + rough walk/drive speed, mirroring
/// the frontend's lib/distance.ts estimate so agent and UI travel-time reasoning stay aligned.
/// </summary>
public sealed class DistanceTool(AppDbContext db) : IAgentTool
{
    private const double EarthRadiusMiles = 3958.8;
    private const double WalkMph = 3;
    private const double DriveMph = 25;
    private const double WalkMaxMiles = 2;

    public string Name => "estimate_travel_time";
    public string Description =>
        "Estimate straight-line distance and travel time (walk or drive) between two places by id. " +
        "Use this before scheduling consecutive stops to make sure start_time + duration_minutes + " +
        "travel buffer for one stop doesn't overlap the next stop's start_time.";

    public object ParameterSchema => new
    {
        type = "object",
        properties = new
        {
            from_place_id = new { type = "string", description = "Place id to travel from" },
            to_place_id = new { type = "string", description = "Place id to travel to" }
        },
        required = new[] { "from_place_id", "to_place_id" }
    };

    public async Task<string> ExecuteAsync(string argumentsJson, string conversationId, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        var root = doc.RootElement;
        var fromId = root.TryGetProperty("from_place_id", out var f) ? f.GetString() : null;
        var toId = root.TryGetProperty("to_place_id", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
            return JsonSerializer.Serialize(new { error = "from_place_id and to_place_id are required" });

        var from = await db.Places.FindAsync([fromId], ct);
        var to = await db.Places.FindAsync([toId], ct);
        if (from is null || to is null)
            return JsonSerializer.Serialize(new { error = "Unknown place id(s)" });

        var miles = HaversineMiles(from.Latitude, from.Longitude, to.Latitude, to.Longitude);
        var mode = miles > WalkMaxMiles ? "drive" : "walk";
        var mph = mode == "walk" ? WalkMph : DriveMph;
        var minutes = Math.Max(1, (int)Math.Round(miles / mph * 60));

        return JsonSerializer.Serialize(new
        {
            from_place_id = fromId,
            to_place_id = toId,
            miles = Math.Round(miles, 1),
            mode,
            estimated_minutes = minutes
        });
    }

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        double ToRad(double d) => d * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusMiles * Math.Asin(Math.Sqrt(a));
    }
}
