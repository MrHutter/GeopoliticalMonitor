using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    WatchlistId = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    TriggerType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastEnabledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRules_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertOccurrences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    AlertRuleId = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProviderRevisionAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    TriggeredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertOccurrences_AlertRules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "AlertRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertOccurrences_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_AlertRuleId_EventId_ProviderRevisionAtUtc",
                table: "AlertOccurrences",
                columns: new[] { "AlertRuleId", "EventId", "ProviderRevisionAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_EventId",
                table: "AlertOccurrences",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_ReadAtUtc_TriggeredAtUtc",
                table: "AlertOccurrences",
                columns: new[] { "ReadAtUtc", "TriggeredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertOccurrences_TriggeredAtUtc_Id",
                table: "AlertOccurrences",
                columns: new[] { "TriggeredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_WatchlistId",
                table: "AlertRules",
                column: "WatchlistId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_WatchlistId_TriggerType",
                table: "AlertRules",
                columns: new[] { "WatchlistId", "TriggerType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertOccurrences");

            migrationBuilder.DropTable(
                name: "AlertRules");
        }
    }
}
