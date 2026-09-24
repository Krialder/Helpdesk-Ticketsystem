using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddKbKategorienTagsUndFreigabe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KategorieId",
                table: "KbArticles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "KbArticles",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewIntervallTage",
                table: "KbArticles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "KbKategorien",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbKategorien", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KbTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NameNormalisiert = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KbVorschlaege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    KategorieId = table.Column<int>(type: "INTEGER", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    VorgeschlagenVon = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VorgeschlagenAm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbVorschlaege", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KbArticleKbTag",
                columns: table => new
                {
                    ArtikelId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KbArticleKbTag", x => new { x.ArtikelId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_KbArticleKbTag_KbArticles_ArtikelId",
                        column: x => x.ArtikelId,
                        principalTable: "KbArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KbArticleKbTag_KbTags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "KbTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KbArticles_KategorieId",
                table: "KbArticles",
                column: "KategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_KbArticleKbTag_TagsId",
                table: "KbArticleKbTag",
                column: "TagsId");

            // Bestand: Die bisherigen Freitext-Kategorien werden je Schreibweise eine
            // feste Kategorie und den Artikeln zugeordnet, bevor die Spalte fällt.
            migrationBuilder.Sql(
                "INSERT INTO KbKategorien (Name) SELECT DISTINCT TRIM(Category) FROM KbArticles " +
                "WHERE Category IS NOT NULL AND TRIM(Category) <> '';");
            migrationBuilder.Sql(
                "UPDATE KbArticles SET KategorieId = (SELECT k.Id FROM KbKategorien k WHERE k.Name = TRIM(KbArticles.Category)) " +
                "WHERE Category IS NOT NULL AND TRIM(Category) <> '';");

            migrationBuilder.AddForeignKey(
                name: "FK_KbArticles_KbKategorien_KategorieId",
                table: "KbArticles",
                column: "KategorieId",
                principalTable: "KbKategorien",
                principalColumn: "Id");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "KbArticles");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KbArticles_KbKategorien_KategorieId",
                table: "KbArticles");

            migrationBuilder.DropTable(
                name: "KbArticleKbTag");

            migrationBuilder.DropTable(
                name: "KbKategorien");

            migrationBuilder.DropTable(
                name: "KbVorschlaege");

            migrationBuilder.DropTable(
                name: "KbTags");

            migrationBuilder.DropIndex(
                name: "IX_KbArticles_KategorieId",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "KategorieId",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "KbArticles");

            migrationBuilder.DropColumn(
                name: "ReviewIntervallTage",
                table: "KbArticles");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "KbArticles",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }
    }
}
