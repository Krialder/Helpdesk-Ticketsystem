using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddRolesPauseAndCreator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Tickets",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByName",
                table: "Tickets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedUntil",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            // Bestand: Bis hierher war der Ersteller implizit der Kunde.
            migrationBuilder.Sql(
                "UPDATE Tickets SET CreatedById = CustomerId, CreatedByName = CustomerName WHERE CreatedById IS NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CreatedByName",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "PausedUntil",
                table: "AspNetUsers");
        }
    }
}
