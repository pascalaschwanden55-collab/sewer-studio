using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Standardhoehe der Eingabefelder unter der Haltungsliste. Abnahmeziel aus dem Nova-Prototyp:
/// Bei 1366 x 768 bleiben mindestens sieben vollstaendige Zeilen sichtbar. Reine Rechnung.
/// </summary>
public static class DataPageWorkspaceLayoutPolicy
{
    public const double MinDrawer = 120;
    public const double SplitterHoehe = 6;
    public const int MindestZeilen = 7;
    private const double Anteil = 0.36;

    /// <summary>
    /// Liefert die Hoehe der Eingabefelder. Eine gespeicherte Hoehe wird uebernommen, aber so
    /// begrenzt, dass Tabellenkopf plus sieben Zeilen sichtbar bleiben. Ist die Flaeche dafuer
    /// zu klein, gilt die Mindesthoehe.
    /// </summary>
    public static double DrawerHeight(double gesamtHoehe, double zeilenHoehe, double kopfHoehe, double? gespeichert)
    {
        var fuerListe = kopfHoehe + MindestZeilen * zeilenHoehe;
        var maxDrawer = gesamtHoehe - fuerListe - SplitterHoehe;
        if (maxDrawer < MinDrawer) return MinDrawer;
        var wunsch = gespeichert is > 0 ? gespeichert.Value : Math.Round(gesamtHoehe * Anteil);
        return Math.Clamp(wunsch, MinDrawer, maxDrawer);
    }
}
