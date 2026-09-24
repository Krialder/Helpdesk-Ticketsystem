using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class EntferneHistorieOffen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistorieOffen",
                table: "Oberflaechenzustaende");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HistorieOffen",
                table: "Oberflaechenzustaende",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
