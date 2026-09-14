using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippy.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "itineraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itineraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pending_actions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    ToolName = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "places",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "text", nullable: false),
                    Neighborhood = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Hours = table.Column<string>(type: "text", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PriceRange = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<double>(type: "double precision", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: false),
                    SeasonalNotes = table.Column<string>(type: "text", nullable: true),
                    BookingRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_places", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "itinerary_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItineraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itinerary_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itinerary_sections_itineraries_ItineraryId",
                        column: x => x.ItineraryId,
                        principalTable: "itineraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "itinerary_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaceId = table.Column<string>(type: "character varying(64)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itinerary_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itinerary_items_itinerary_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "itinerary_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itinerary_items_places_PlaceId",
                        column: x => x.PlaceId,
                        principalTable: "places",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_itinerary_items_PlaceId",
                table: "itinerary_items",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_itinerary_items_SectionId",
                table: "itinerary_items",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_itinerary_sections_ItineraryId",
                table: "itinerary_sections",
                column: "ItineraryId");

            migrationBuilder.CreateIndex(
                name: "IX_pending_actions_ConversationId",
                table: "pending_actions",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_places_City",
                table: "places",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_places_Name",
                table: "places",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_places_Type",
                table: "places",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itinerary_items");

            migrationBuilder.DropTable(
                name: "pending_actions");

            migrationBuilder.DropTable(
                name: "itinerary_sections");

            migrationBuilder.DropTable(
                name: "places");

            migrationBuilder.DropTable(
                name: "itineraries");
        }
    }
}
