using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddWissensKontokennungen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VorgeschlagenVonId",
                table: "KbVorschlaege",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "KbArticles",
                type: "TEXT",
                maxLength: 450,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VorgeschlagenVonId",
                table: "KbVorschlaege");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "KbArticles");
        }
    }
}
