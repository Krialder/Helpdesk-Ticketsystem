using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ticketsystem.Kern.Start;

// Ein WinExe hat keine Konsole. Wer die Anwendung aus einem Terminal mit
// „--sichtprobe“ oder „--startprobe“ startet, soll die Ausgabe trotzdem
// sehen; dafür hängt sich der Prozess an die Konsole des Elternprozesses.
// Ohne Eltern-Konsole (Doppelklick) gibt es nichts zu tun.
public static class Elternkonsole
{
    private const int ElternProzess = -1;

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int prozessId);

    public static bool Anhaengen()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (!AttachConsole(ElternProzess))
        {
            return false;
        }

        // Nach AttachConsole zeigt Console.Out noch auf einen Null-Schreiber; erst
        // ein neuer Writer auf den Standardstrom schreibt wirklich.
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        return true;
    }
}
