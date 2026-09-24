using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class EntferneNurMeine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NurMeine",
                table: "Oberflaechenzustaende");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NurMeine",
                table: "Oberflaechenzustaende",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
