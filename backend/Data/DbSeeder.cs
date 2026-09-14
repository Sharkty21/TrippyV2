using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data.Entities;

namespace Trippy.Backend.Data;

/// <summary>
/// Loads Places from Data/italy.json and a sample Rome itinerary when tables are empty.
/// To add seed places: append objects to italy.json (same shape as existing rows).
/// To change the demo trip: edit SeedSampleItineraryAsync below.
/// </summary>
public sealed class DbSeeder(AppDbContext db, ILogger<DbSeeder> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Applies any pending migrations (and creates the DB/schema on first run).
        // Using migrations instead of EnsureCreated lets us evolve the schema
        // (e.g. adding new columns) without wiping the database.
        await db.Database.MigrateAsync(ct);

        if (!await db.Places.AnyAsync(ct))
        {
            await SeedPlacesAsync(ct);
            logger.LogInformation("Seeded places from italy.json");
        }

        if (!await db.Itineraries.AnyAsync(ct))
        {
            await SeedSampleItineraryAsync(ct);
            logger.LogInformation("Seeded sample Rome Highlights itinerary");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPlacesAsync(CancellationToken ct)
    {
        var path = ResolveItalyJsonPath();
        if (!File.Exists(path))
        {
            logger.LogWarning("italy.json not found at {Path}; skipping place seed", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        var rows = await JsonSerializer.DeserializeAsync<List<PlaceSeedRow>>(stream, JsonOptions, ct)
                   ?? [];

        foreach (var row in rows)
        {
            db.Places.Add(new Place
            {
                Id = row.Id,
                Name = row.Name,
                Type = row.Type,
                City = row.City,
                Region = row.Region,
                Neighborhood = row.Neighborhood,
                Description = row.Description ?? string.Empty,
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                Hours = row.Hours,
                DurationMinutes = row.DurationMinutes,
                PriceRange = row.PriceRange,
                Rating = row.Rating,
                Tags = row.Tags ?? [],
                SeasonalNotes = row.SeasonalNotes,
                BookingRequired = row.BookingRequired ?? false
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSampleItineraryAsync(CancellationToken ct)
    {
        var itin = new Itinerary
        {
            Name = "Rome Highlights",
            Description = "A classic three-day taste of Rome — ancient sites, markets, and Trastevere evenings.",
            StartDate = new DateOnly(2026, 6, 16),
            EndDate = new DateOnly(2026, 6, 18)
        };
        db.Itineraries.Add(itin);
        await db.SaveChangesAsync(ct);

        var day1 = new ItinerarySection
        {
            ItineraryId = itin.Id,
            Date = new DateOnly(2026, 6, 16),
            Description = "Ancient Rome: Colosseum & Forum",
            Sequence = 0
        };
        var day2 = new ItinerarySection
        {
            ItineraryId = itin.Id,
            Date = new DateOnly(2026, 6, 17),
            Description = "Centro storico: Pantheon, piazzas & gelato",
            Sequence = 1
        };
        var day3 = new ItinerarySection
        {
            ItineraryId = itin.Id,
            Date = new DateOnly(2026, 6, 18),
            Description = "Trastevere wander & dinner",
            Sequence = 2
        };
        db.Sections.AddRange(day1, day2, day3);
        await db.SaveChangesAsync(ct);

        db.Items.AddRange(
            new ItineraryItem { SectionId = day1.Id, PlaceId = "place_001", Description = "Go early; underground ticket if available.", Sequence = 0 },
            new ItineraryItem { SectionId = day1.Id, PlaceId = "place_004", Description = "Same ticket as Colosseum.", Sequence = 1 },
            new ItineraryItem { SectionId = day1.Id, PlaceId = "place_005", Description = "Quick stop under the oculus.", Sequence = 2 },
            new ItineraryItem { SectionId = day2.Id, PlaceId = "place_006", Description = "Morning market before it clears.", Sequence = 0 },
            new ItineraryItem { SectionId = day2.Id, PlaceId = "place_008", Description = "Fountain of the Four Rivers.", Sequence = 1 },
            new ItineraryItem { SectionId = day2.Id, PlaceId = "place_007", Description = "Book timed entry months ahead.", Sequence = 2 },
            new ItineraryItem { SectionId = day3.Id, PlaceId = "place_002", Description = "Golden-hour stroll.", Sequence = 0 },
            new ItineraryItem { SectionId = day3.Id, PlaceId = "place_003", Description = "Reserve or arrive early.", Sequence = 1 });

        await db.SaveChangesAsync(ct);
    }

    private static string ResolveItalyJsonPath()
    {
        // Prefer output folder next to the running assembly, then walk up for source tree during development.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "italy.json"),
            Path.Combine(AppContext.BaseDirectory, "italy.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "italy.json"))
        };
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private sealed class PlaceSeedRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string City { get; set; } = "";
        public string Region { get; set; } = "";
        public string? Neighborhood { get; set; }
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Hours { get; set; }
        public int? DurationMinutes { get; set; }
        public string? PriceRange { get; set; }
        public double? Rating { get; set; }
        public List<string>? Tags { get; set; }
        public string? SeasonalNotes { get; set; }
        public bool? BookingRequired { get; set; }
    }
}
