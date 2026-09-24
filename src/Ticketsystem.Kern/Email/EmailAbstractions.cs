namespace Ticketsystem.Kern.Email;

public class EmailOptions
{
    public bool Enabled { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string MailboxAddress { get; set; } = string.Empty;
}

// Der eine Versandweg der Anwendung; ohne Email:Enabled schreibt der
// LoggingTicketMailer statt zu senden, und die Anwendung läuft vollständig
// ohne Zugangsdaten.
public interface ITicketMailer
{
    Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken);
}

public class LoggingTicketMailer(ILogger<LoggingTicketMailer> logger) : ITicketMailer
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("E-Mail-Versand deaktiviert (Email:Enabled=false). An {To}: {Subject}",
            Maskieren(to), subject);
        return Task.CompletedTask;
    }

    // Ins Protokoll kommt keine ganze Adresse: Das Protokoll liegt als Datei
    // auf dem Rechner.
    private static string Maskieren(string adresse)
    {
        var at = adresse.IndexOf('@');
        return at <= 1 ? "***" : $"{adresse[0]}***{adresse[at..]}";
    }
}
