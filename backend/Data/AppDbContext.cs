using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Trippy.Backend.Data.Entities;

namespace Trippy.Backend.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Itinerary> Itineraries => Set<Itinerary>();
    public DbSet<ItinerarySection> Sections => Set<ItinerarySection>();
    public DbSet<ItineraryItem> Items => Set<ItineraryItem>();
    public DbSet<PendingAction> PendingActions => Set<PendingAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Place>(e =>
        {
            e.ToTable("places");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.City);
            e.Property(x => x.Tags)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                    v => v.ToList()));
        });

        modelBuilder.Entity<Itinerary>(e =>
        {
            e.ToTable("itineraries");
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Sections)
                .WithOne(x => x.Itinerary)
                .HasForeignKey(x => x.ItineraryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItinerarySection>(e =>
        {
            e.ToTable("itinerary_sections");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ItineraryId);
            e.HasMany(x => x.Items)
                .WithOne(x => x.Section)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItineraryItem>(e =>
        {
            e.ToTable("itinerary_items");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SectionId);
            e.HasIndex(x => x.PlaceId);
            e.HasOne(x => x.Place)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PlaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PendingAction>(e =>
        {
            e.ToTable("pending_actions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ConversationId);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
        });
    }
}
