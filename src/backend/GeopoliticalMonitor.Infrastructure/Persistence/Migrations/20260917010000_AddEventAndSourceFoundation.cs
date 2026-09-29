using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations;

public partial class AddEventAndSourceFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Events",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Latitude = table.Column<double>(type: "REAL", nullable: false),
                Longitude = table.Column<double>(type: "REAL", nullable: false),
                LocationDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                CountryCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                Region = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Events", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SourceItems",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                SourceType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ExternalId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                Text = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                Url = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                AuthorDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                AuthorHandle = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                PublishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RetrievedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Latitude = table.Column<double>(type: "REAL", nullable: true),
                Longitude = table.Column<double>(type: "REAL", nullable: true),
                LocationDisplayName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                CountryCode = table.Column<string>(type: "TEXT", maxLength: 8, nullable: true),
                Region = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SourceItems", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EventSourceAssociations",
            columns: table => new
            {
                EventId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                SourceItemId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventSourceAssociations", x => new { x.EventId, x.SourceItemId });
                table.ForeignKey("FK_EventSourceAssociations_Events_EventId", x => x.EventId, "Events", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_EventSourceAssociations_SourceItems_SourceItemId", x => x.SourceItemId, "SourceItems", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EventSourceAssociations_SourceItemId",
            table: "EventSourceAssociations",
            column: "SourceItemId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EventSourceAssociations");
        migrationBuilder.DropTable(name: "Events");
        migrationBuilder.DropTable(name: "SourceItems");
    }
}