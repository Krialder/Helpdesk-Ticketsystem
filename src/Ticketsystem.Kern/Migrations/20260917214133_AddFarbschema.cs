using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddFarbschema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Farbschema",
                table: "Oberflaechenzustaende",
                type: "TEXT",
                nullable: false,
                defaultValue: "dunkel");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Farbschema",
                table: "Oberflaechenzustaende");
        }
    }
}
