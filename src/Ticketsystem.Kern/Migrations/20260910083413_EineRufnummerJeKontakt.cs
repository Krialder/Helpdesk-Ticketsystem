using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class EineRufnummerJeKontakt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LetzteRufnummer",
                table: "Kontakte",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            // Von der Liste der Rufnummern bleibt die zuletzt genannte; der Rest
            // fällt mit der Tabelle (DSGVO-Sparsamkeit).
            migrationBuilder.Sql(
                """
                UPDATE Kontakte
                SET LetzteRufnummer = (
                    SELECT r.Nummer
                    FROM KontaktRufnummern r
                    WHERE r.KontaktId = Kontakte.Id
                    ORDER BY r.CreatedAt DESC, r.Id DESC
                    LIMIT 1
                );
                """);

            migrationBuilder.DropTable(
                name: "KontaktRufnummern");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KontaktRufnummern",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    KontaktId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KontaktRufnummern", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KontaktRufnummern_Kontakte_KontaktId",
                        column: x => x.KontaktId,
                        principalTable: "Kontakte",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KontaktRufnummern_KontaktId",
                table: "KontaktRufnummern",
                column: "KontaktId");

            // Rückweg: Die eine Nummer wird wieder eine Zeile; die Art wird aus der
            // Länge geschätzt, weil sie nicht mehr gespeichert ist.
            migrationBuilder.Sql(
                """
                INSERT INTO KontaktRufnummern (KontaktId, Nummer, Art, CreatedAt)
                SELECT Id,
                       LetzteRufnummer,
                       CASE
                           WHEN length(LetzteRufnummer) <= 6 THEN 0
                           WHEN LetzteRufnummer LIKE '01%' THEN 3
                           ELSE 1
                       END,
                       UpdatedAt
                FROM Kontakte
                WHERE LetzteRufnummer IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "LetzteRufnummer",
                table: "Kontakte");
        }
    }
}
