using Azure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Ticketsystem.Kern.Data;
using Ticketsystem.Kern.Domain;
using Ticketsystem.Kern.Email;
using Ticketsystem.Kern.Praesentation;
using Ticketsystem.Kern.Services;

namespace Ticketsystem.Kern.Start;

// Die eine Dienstverdrahtung für Anwendung und Tests: Wer den Host anders
// baut, ruft dieselbe Methode, damit kein Dienst nur in einem der beiden
// fehlt.
public static class KernDienste
{
    public static IServiceCollection Registrieren(
        IServiceCollection services, IConfiguration configuration, string? einstellungsdatei = null)
    {
        services.AddDbContext<TicketsystemContext>(options =>
            options.UseSqlite(Datenablage.Verbindung(configuration)));

        services
            .AddIdentityCore<AppUser>()
            .AddRoles<IdentityRole>()
            .AddErrorDescriber<DeutscheIdentityFehler>()
            .AddEntityFrameworkStores<TicketsystemContext>()
            // AddIdentityCore registriert keinen Token-Provider; ohne diesen wirft das
            // Zurücksetzen eines Passworts über die Verwaltung.
            .AddTokenProvider<EmailTokenProvider<AppUser>>(TokenOptions.DefaultProvider);

        services.AddScoped<TicketService>();
        services.AddScoped<ZustandService>();
        services.AddScoped<Namensverzeichnis>();
        services.AddScoped<KontokartePresenter>();
        services.AddScoped<KnowledgeBaseService>();
        services.AddScoped<FassungenPresenter>();
        services.AddScoped<WissensPresenter>();
        services.AddScoped<StammdatenService>();
        services.AddScoped<RuhezeitService>();
        services.AddScoped<SlaMelder>();
        services.AddScoped<SicherungService>();
        services.AddScoped<BestaetigungsMelder>();
        services.AddScoped<AustauschService>();
        services.AddScoped<AuswertungService>();
        services.AddScoped<AnmeldeDienst>();
        services.AddScoped<KontenDienst>();
        services.Configure<DatenOptions>(configuration.GetSection("Daten"));
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<SlaOptions>(configuration.GetSection("Sla"));
        services.AddSingleton<Sicherungsstand>();
        services.AddSingleton(sp => new Einstellungsablage(
            einstellungsdatei ?? Einstellungsablage.Vorgabepfad(),
            sp.GetRequiredService<IOptions<DatenOptions>>(),
            configuration));
        services.AddHostedService<SlaMelderService>();
        services.AddHostedService<SicherungsDienst>();

        // Ohne Email:Enabled kein Graph-Client: Die Anwendung läuft vollständig
        // ohne Zugangsdaten, der Versand wird protokolliert statt gesendet.
        if (configuration.GetValue<bool>("Email:Enabled"))
        {
            services.AddSingleton(_ =>
            {
                var email = configuration.GetSection("Email").Get<EmailOptions>()!;
                return new GraphServiceClient(
                    new ClientSecretCredential(email.TenantId, email.ClientId, email.ClientSecret),
                    ["https://graph.microsoft.com/.default"]);
            });
            services.AddSingleton<ITicketMailer, GraphTicketMailer>();
        }
        else
        {
            services.AddSingleton<ITicketMailer, LoggingTicketMailer>();
        }

        return services;
    }
}
