using System.Globalization;
using System.Text;

namespace Ticketsystem.Kern.Start;

// Alles, was auf die Konsole geht, geht zusätzlich in eine Tagesdatei im
// Datenordner, damit ein Fehler vom Vortag noch nachlesbar ist; alte
// Dateien werden beim Start aufgeräumt.
public static class Protokolldatei
{
    public const string Ordnername = "Protokoll";

    public const int VorgabeTage = 14;

    public static string Einrichten(string datenbankDatei, int aufbewahrtTage)
    {
        var ordner = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(datenbankDatei)) ?? ".",
            Ordnername);
        Directory.CreateDirectory(ordner);

        var pfad = Path.Combine(
            ordner,
            $"ticketsystem-{DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.log");

        var schreiber = new StreamWriter(pfad, append: true) { AutoFlush = true };

        schreiber.WriteLine(
            $"# Ticketsystem gestartet {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} UTC. Alle Zeitangaben in UTC.");

        Console.SetOut(new Doppelschreiber(Console.Out, schreiber));
        Console.SetError(new Doppelschreiber(Console.Error, schreiber));

        Aufraeumen(ordner, aufbewahrtTage);
        return pfad;
    }

    // Schreibt in zwei Ziele zugleich; Encoding ist das der Datei.
    private sealed class Doppelschreiber(TextWriter einer, TextWriter anderer) : TextWriter
    {
        public override Encoding Encoding => anderer.Encoding;

        public override void Write(char zeichen)
        {
            einer.Write(zeichen);
            anderer.Write(zeichen);
        }

        public override void Write(string? text)
        {
            einer.Write(text);
            anderer.Write(text);
        }

        public override void WriteLine(string? text)
        {
            einer.WriteLine(text);
            anderer.WriteLine(text);
        }

        public override void Flush()
        {
            einer.Flush();
            anderer.Flush();
        }
    }

    public static int Aufraeumen(string ordner, int aufbewahrtTage)
    {
        if (!Directory.Exists(ordner) || aufbewahrtTage <= 0)
        {
            return 0;
        }

        var grenze = DateTime.UtcNow.AddDays(-aufbewahrtTage);
        var alte = new DirectoryInfo(ordner)
            .GetFiles("ticketsystem-*.log")
            .Where(datei => datei.LastWriteTimeUtc < grenze)
            .ToList();

        foreach (var datei in alte)
        {
            try
            {
                datei.Delete();
            }
            // Eine gesperrte Datei (Virenscanner, zweiter Prozess) bricht den Start
            // nicht ab; sie fällt beim nächsten Mal.
            catch (IOException)
            {
            }
        }

        return alte.Count;
    }
}
