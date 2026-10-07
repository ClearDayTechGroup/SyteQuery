using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyteQuery.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableIdoName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdoDefinitions");

            // Environments that existed before this migration all ran their queries through the one
            // IDO the app used to install itself, so that's the right value for existing rows.
            migrationBuilder.AddColumn<string>(
                name: "IdoName",
                table: "UserEnvironments",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "ue_RC_QueryTool");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdoName",
                table: "UserEnvironments");

            migrationBuilder.CreateTable(
                name: "IdoDefinitions",
                columns: table => new
                {
                    IdoName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevisionNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    XmlDefinition = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdoDefinitions", x => x.IdoName);
                });
        }
    }
}
