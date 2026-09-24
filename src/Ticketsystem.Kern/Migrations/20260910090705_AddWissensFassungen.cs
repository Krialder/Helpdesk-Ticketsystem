using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddWissensFassungen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KbFassungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nummer = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Kategorie = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Published = table.Column<bool>(type: "INTEGER", nullable: false),
                    Anlass = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    GespeichertVon = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GespeichertVonId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    GespeichertAm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbFassungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KbFassungen_KbArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KbArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KbFassungen_ArticleId_Nummer",
                table: "KbFassungen",
                columns: new[] { "ArticleId", "Nummer" },
                unique: true);

            // Jeder bestehende Artikel bekommt seinen Stand als Fassung 1 („Stand vor
            // F3“), mit Kategorie und Tags als eingefrorenen Namen; ein Artikel ohne
            // Vergangenheit sähe in der Versionsliste aus wie ein Fehler.
            migrationBuilder.Sql(
                """
                INSERT INTO KbFassungen
                    (ArticleId, Nummer, Title, Content, Kategorie, Tags, Published, Anlass,
                     GespeichertVon, GespeichertVonId, GespeichertAm)
                SELECT a.Id, 1, a.Title, a.Content, k.Name,
                       COALESCE((SELECT group_concat(t.Name, ', ')
                                 FROM (SELECT t2.Name
                                       FROM KbArticleKbTag at
                                       JOIN KbTags t2 ON t2.Id = at.TagsId
                                       WHERE at.ArtikelId = a.Id
                                       ORDER BY t2.Name COLLATE NOCASE) t), ''),
                       a.Published, 'Stand vor F3',
                       COALESCE(a.Owner, 'unbekannt'), NULL, a.UpdatedAt
                FROM KbArticles a
                LEFT JOIN KbKategorien k ON k.Id = a.KategorieId;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KbFassungen");
        }
    }
}
