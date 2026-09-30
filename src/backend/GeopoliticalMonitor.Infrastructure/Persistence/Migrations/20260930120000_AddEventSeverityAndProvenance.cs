using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventSeverityAndProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Events",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<string>(
                name: "ProviderKey",
                table: "Events",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "Events",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Low");

            migrationBuilder.AddColumn<string>(
                name: "SeverityInputs",
                table: "Events",
                type: "TEXT",
                maxLength: 256,
                nullable: false,
                defaultValue: "backfill: pending");

            migrationBuilder.AddColumn<string>(
                name: "SeverityRule",
                table: "Events",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "legacy-default");

            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "Provider" = 'Gdacs', "ProviderKey" = substr("Id", 14)
                WHERE substr("Id", 1, 13) = 'evt-gdacs-eq-'
                  AND length("Id") > 13
                  AND substr("Id", 14) NOT GLOB '*[^0-9]*';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "Provider" = 'Demo', "ProviderKey" = NULL
                WHERE substr("Id", 1, 9) = 'evt-demo-';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Events_Provider_ProviderKey",
                table: "Events",
                columns: new[] { "Provider", "ProviderKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_Provider_ProviderKey",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ProviderKey",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SeverityInputs",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SeverityRule",
                table: "Events");
        }
    }
}
