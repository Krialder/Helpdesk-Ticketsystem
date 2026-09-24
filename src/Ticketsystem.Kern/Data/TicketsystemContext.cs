using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ticketsystem.Kern.Domain;

namespace Ticketsystem.Kern.Data;

// Der EF-Kontext über SQLite. Die Datenbank kennt kein rowversion; das
// Konkurrenzmerkmal der Tickets vergibt deshalb der Kontext beim Speichern
// selbst (StempelSetzen).
public class TicketsystemContext(DbContextOptions<TicketsystemContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketHistoryEntry> TicketHistory => Set<TicketHistoryEntry>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    public DbSet<KbArticle> KbArticles => Set<KbArticle>();

    public DbSet<Kontakt> Kontakte => Set<Kontakt>();

    public DbSet<Ruhezeit> Ruhezeiten => Set<Ruhezeit>();

    public DbSet<KbKategorie> KbKategorien => Set<KbKategorie>();

    public DbSet<KbTag> KbTags => Set<KbTag>();

    public DbSet<KbAenderungsvorschlag> KbVorschlaege => Set<KbAenderungsvorschlag>();

    public DbSet<KbArtikelFassung> KbFassungen => Set<KbArtikelFassung>();

    public DbSet<Oberflaechenzustand> Oberflaechenzustaende => Set<Oberflaechenzustand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Oberflaechenzustand>().HasKey(z => z.KontoId);

        modelBuilder.Entity<Ticket>()
            .Property(t => t.RowVersion)
            .IsConcurrencyToken();

        // Die Indizes folgen den Filtern und Sortierungen der Liste; Adresse und
        // Kundenname tragen die Raum- und Personenhistorie.
        modelBuilder.Entity<Ticket>().HasIndex(t => new { t.Status, t.CreatedAt });
        modelBuilder.Entity<Ticket>().HasIndex(t => t.CreatedAt);
        modelBuilder.Entity<Ticket>().HasIndex(t => t.Address);
        modelBuilder.Entity<Ticket>().HasIndex(t => t.CustomerName);

        modelBuilder.Entity<KbArtikelFassung>()
            .HasIndex(f => new { f.ArticleId, f.Nummer })
            .IsUnique();
        modelBuilder.Entity<KbArticle>()
            .HasMany(a => a.Fassungen)
            .WithOne()
            .HasForeignKey(f => f.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StempelSetzen();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StempelSetzen();
        return base.SaveChanges();
    }

    // Ein neuer Wert bei jedem Speichern: Wer noch den alten Stand in der Hand
    // hat, wird von EF abgewiesen, statt ihn zu überschreiben.
    private void StempelSetzen()
    {
        foreach (var eintrag in ChangeTracker.Entries<Ticket>())
        {
            if (eintrag.State is EntityState.Added or EntityState.Modified)
            {
                eintrag.Entity.RowVersion = Guid.NewGuid();
            }
        }
    }
}
