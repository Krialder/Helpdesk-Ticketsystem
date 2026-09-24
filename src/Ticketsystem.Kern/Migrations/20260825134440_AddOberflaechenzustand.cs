using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddOberflaechenzustand : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Oberflaechenzustaende",
                columns: table => new
                {
                    KontoId = table.Column<string>(type: "TEXT", nullable: false),
                    Ansicht = table.Column<string>(type: "TEXT", nullable: false),
                    Prioritaet = table.Column<int>(type: "INTEGER", nullable: false),
                    NurMeine = table.Column<bool>(type: "INTEGER", nullable: false),
                    Sortierung = table.Column<int>(type: "INTEGER", nullable: false),
                    Absteigend = table.Column<bool>(type: "INTEGER", nullable: false),
                    VerwaltungsReiter = table.Column<int>(type: "INTEGER", nullable: false),
                    HistorieOffen = table.Column<bool>(type: "INTEGER", nullable: false),
                    FensterBreite = table.Column<double>(type: "REAL", nullable: false),
                    FensterHoehe = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oberflaechenzustaende", x => x.KontoId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Oberflaechenzustaende");
        }
    }
}
