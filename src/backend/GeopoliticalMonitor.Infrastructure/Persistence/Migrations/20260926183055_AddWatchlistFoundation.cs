using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchlistFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistEvents",
                columns: table => new
                {
                    WatchlistId = table.Column<string>(type: "TEXT", maxLength: 35, nullable: false),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistEvents", x => new { x.WatchlistId, x.EventId });
                    table.ForeignKey(
                        name: "FK_WatchlistEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchlistEvents_Watchlists_WatchlistId",
                        column: x => x.WatchlistId,
                        principalTable: "Watchlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEvents_EventId",
                table: "WatchlistEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_NormalizedName",
                table: "Watchlists",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchlistEvents");

            migrationBuilder.DropTable(
                name: "Watchlists");
        }
    }
}
