using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyteQuery.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TokenMode",
                table: "UserEnvironments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenMode",
                table: "UserEnvironments");
        }
    }
}
