using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeopoliticalMonitor.Infrastructure.Persistence.Migrations;

public partial class InitialLocalPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApplicationPreferences",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationPreferences", x => x.Key);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ApplicationPreferences");
    }
}
