using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ticketsystem.Kern.Data;

// Nur für „dotnet ef migrations“: Das Werkzeug braucht einen Kontext ohne
// den Host, und die Datei entwurfszeit.db wird nie im Betrieb benutzt.
public sealed class EntwurfszeitFabrik : IDesignTimeDbContextFactory<TicketsystemContext>
{
    public TicketsystemContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<TicketsystemContext>()
            .UseSqlite("Data Source=entwurfszeit.db")
            .Options);
}
