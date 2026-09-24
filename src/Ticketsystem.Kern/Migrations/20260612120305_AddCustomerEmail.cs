using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ticketsystem.Kern.Migrations
{
    public partial class AddCustomerEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "Tickets",
                type: "TEXT",
                maxLength: 320,
                nullable: true);

            // Bestand: Bei E-Mail-Vorgängen und bei Vorgängen angemeldeter Kunden
            // stand die Adresse bisher im Kundennamen.
            migrationBuilder.Sql(
                "UPDATE Tickets SET CustomerEmail = CustomerName WHERE Source = 1 OR (Source = 0 AND CustomerId IS NOT NULL);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "Tickets");
        }
    }
}
