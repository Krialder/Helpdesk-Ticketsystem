using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddKontaktStammdaten : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kontakte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NameNormalisiert = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LetzteAdresse = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kontakte", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KontaktRufnummern",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KontaktId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Art = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KontaktRufnummern");

            migrationBuilder.DropTable(
                name: "Kontakte");
        }
    }
}
