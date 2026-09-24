using Avalonia;
using Avalonia.Controls;

namespace Ticketsystem.Tests;

// Misst Lage und Ausdehnung eines Bedienelements im Koordinatensystem des
// Fensters. Bounds allein sind relativ zum jeweiligen Elternteil; ob zwei
// Elemente fluchten, zeigt sich erst im gemeinsamen System.
internal static class Messen
{
    public static Rect Kante(Window fenster, Control element)
    {
        var oben = element.TranslatePoint(new Point(0, 0), fenster)
            ?? throw new InvalidOperationException($"{element.Name ?? element.GetType().Name} hängt nicht im Fenster.");
        return new Rect(oben, element.Bounds.Size);
    }

    public static void Auslegen(Window fenster, double breite, double hoehe)
    {
        fenster.Show();
        fenster.Measure(new Size(breite, hoehe));
        fenster.Arrange(new Rect(0, 0, breite, hoehe));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }
}
