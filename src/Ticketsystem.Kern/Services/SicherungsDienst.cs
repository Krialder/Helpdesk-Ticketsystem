using Microsoft.Extensions.Options;

namespace Ticketsystem.Kern.Services;

// Fragt in festem Takt nach einer fälligen Sicherung, solange die Anwendung
// läuft; ob gesichert wird, entscheidet SicherungService am Alter des
// jüngsten Standes. Ohne diesen Dienst wäre der Prozessstart der einzige
// Auslöser, und wer den Laptop nur zuklappt, hätte nach einer Woche eine
// Woche Arbeit allein in der Datenbankdatei.
public class SicherungsDienst(
    IServiceScopeFactory scopeFactory,
    IOptions<DatenOptions> optionen,
    ILogger<SicherungsDienst> logger)
    : BackgroundService
{
    // Viertelstündlich, damit ein Intervall von vier Stunden auf eine
    // Viertelstunde genau eingehalten wird.
    private static readonly TimeSpan Takt = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (optionen.Value.SicherungIntervallStunden <= 0)
        {
            logger.LogInformation("Laufende Sicherung ist abgeschaltet (Daten:SicherungIntervallStunden).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sicherung = scope.ServiceProvider.GetRequiredService<SicherungService>();
                await sicherung.FaelligeSicherungAsync(DateTime.UtcNow);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Die laufende Sicherung ist unerwartet gescheitert.");
            }

            try
            {
                await Task.Delay(Takt, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
