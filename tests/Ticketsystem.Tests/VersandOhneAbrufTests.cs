using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ticketsystem.Kern.Email;

namespace Ticketsystem.Tests;

// Email:Enabled schaltet nur die Fristen-Mails ein, nicht den
// Hintergrunddienst, der das Postfach abholt und jede Mail ungefragt in ein
// Ticket übersetzt. Der Beschluss „Postfach wird von Hand gelesen" muss auch
// mit eingeschaltetem Versand gelten.
public sealed class VersandOhneAbrufTests : IDisposable
{
    private KernWirt? _factory;

    // Attrappen-Zugangsdaten: Die Graph-Klassen werden nur registriert und
    // gebaut; kein Test hier verschickt etwas oder greift ins Netz.
    private KernWirt MitVersand()
    {
        _factory = new KernWirt();
        _factory.MitEinstellungen(new()
        {
            ["Email:Enabled"] = "true",
            ["Email:TenantId"] = "attrappe",
            ["Email:ClientId"] = "attrappe",
            ["Email:ClientSecret"] = "attrappe",
            ["Email:Mailbox"] = "helpdesk@attrappe.local"
        });
        return _factory;
    }

    [Fact]
    public void Mit_eingeschaltetem_Versand_startet_kein_Postfachabruf()
    {
        var dienste = MitVersand().Services.GetServices<IHostedService>().ToList();

        Assert.DoesNotContain(dienste, d =>
            d.GetType().Name.Contains("Polling", StringComparison.OrdinalIgnoreCase)
            || d.GetType().Name.Contains("Intake", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Der_Schalter_bedeutet_nur_noch_Versand()
    {
        using var scope = MitVersand().Services.CreateScope();

        var mailer = scope.ServiceProvider.GetRequiredService<ITicketMailer>();

        Assert.Equal("GraphTicketMailer", mailer.GetType().Name);
    }

    [Fact]
    public void Ohne_den_Schalter_wird_nur_protokolliert()
    {
        _factory = new KernWirt();
        using var scope = _factory.Services.CreateScope();

        var mailer = scope.ServiceProvider.GetRequiredService<ITicketMailer>();

        Assert.Equal("LoggingTicketMailer", mailer.GetType().Name);
    }

    // Entfernt statt stillgelegt: Ein toter Pfad, der samt Tests weiterlebt,
    // wird beim nächsten Umbau mitgepflegt und irgendwann versehentlich
    // wiederbelebt.
    [Fact]
    public void Der_Abruf_ist_aus_dem_Quelltext_verschwunden()
    {
        var dateien = Directory.GetFiles(
            Path.Combine(Quellordner(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(datei => !datei.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !datei.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        string[] reste = ["IMailbox", "IncomingEmail", "EmailPollingService", "EmailIntakeService", "PollSeconds"];
        var treffer = dateien
            .Where(datei => reste.Any(rest => File.ReadAllText(datei).Contains(rest, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(treffer);
    }

    private static string Quellordner()
    {
        var ordner = AppContext.BaseDirectory;
        while (ordner is not null && !File.Exists(Path.Combine(ordner, "Ticketsystem.sln")))
        {
            ordner = Directory.GetParent(ordner)?.FullName;
        }

        return ordner ?? throw new InvalidOperationException("Die Projektwurzel wurde nicht gefunden.");
    }

    public void Dispose() => _factory?.Dispose();
}
