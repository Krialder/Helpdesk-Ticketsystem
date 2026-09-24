using Ticketsystem.Kern.Start;

namespace Ticketsystem.Tests;

// Ein Startfehler darf nie wortlos sein: Protokolldatei.Einrichten verdoppelt
// die Konsolenausgabe in die Datei und ersetzt sie nicht. Einmal stand die
// ganze Stapelspur in einer Protokolldatei, von der niemand wusste, und auf
// der Konsole nur „Code 1".
public sealed class StartsichtbarkeitTests : IDisposable
{
    private readonly string _ordner = Path.Combine(
        Path.GetTempPath(), $"startsicht-{Guid.NewGuid():N}");

    private readonly TextWriter _ausVorher = Console.Out;
    private readonly TextWriter _fehlerVorher = Console.Error;

    public StartsichtbarkeitTests() => Directory.CreateDirectory(_ordner);

    [Fact]
    public void Die_Konsolenausgabe_geht_in_die_Datei_UND_bleibt_auf_der_Konsole()
    {
        var mitleser = new StringWriter();
        Console.SetOut(mitleser);
        Console.SetError(mitleser);

        var pfad = Protokolldatei.Einrichten(Path.Combine(_ordner, "app.db"), aufbewahrtTage: 14);
        Console.WriteLine("Zeile auf stdout.");
        Console.Error.WriteLine("Zeile auf stderr.");
        Console.Out.Flush();
        Console.Error.Flush();

        var inDerDatei = File.ReadAllText(pfad);
        var aufDerKonsole = mitleser.ToString();

        Assert.Contains("Zeile auf stdout.", inDerDatei);
        Assert.Contains("Zeile auf stderr.", inDerDatei);
        Assert.Contains("Zeile auf stdout.", aufDerKonsole);
        Assert.Contains("Zeile auf stderr.", aufDerKonsole);
    }

    [Fact]
    public void Die_Kopfzeile_nennt_den_Zeitbezug()
    {
        Console.SetOut(new StringWriter());
        var pfad = Protokolldatei.Einrichten(Path.Combine(_ordner, "app.db"), aufbewahrtTage: 14);

        Assert.Contains("Alle Zeitangaben in UTC", File.ReadAllText(pfad));
    }

    // Ohne den Wächter in Elternkonsole endet das P/Invoke nach kernel32 auf dem
    // Linux-Prüfrechner in einer DllNotFoundException, also in genau der
    // Fehlerklasse, die die Datei beheben soll.
    [Fact]
    public void Das_Anhaengen_der_Elternkonsole_wirft_auf_keinem_System()
    {
        var haengt = Elternkonsole.Anhaengen();

        Assert.True(haengt || OperatingSystem.IsWindows(),
            "Ausserhalb von Windows haengt die Konsole immer schon.");
    }

    public void Dispose()
    {
        Console.SetOut(_ausVorher);
        Console.SetError(_fehlerVorher);
        if (Directory.Exists(_ordner))
        {
            Directory.Delete(_ordner, recursive: true);
        }
    }
}
