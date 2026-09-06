using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Ergebnis der Hoehenregel: Hoehe der Eingabefelder oder Anweisung, sie zuzuklappen.</summary>
public readonly record struct DataPageDrawerLayout(double Hoehe, bool Zugeklappt);

/// <summary>
/// Standardhoehe der Eingabefelder unter der Haltungsliste. Abnahmeziel aus dem Nova-Prototyp:
/// Bei 1366 x 768 bleiben mindestens sieben vollstaendige Zeilen sichtbar. Reine Rechnung.
/// Die Sichtprobe am Programm (06.09.2026) zeigte, dass die Werkzeugzeilen ueber der Liste bei
/// 1366 x 768 nur rund 400 px fuer Liste und Eingabefelder lassen. Reicht der Platz nicht fuer
/// sieben Zeilen plus Mindesthoehe, werden die Eingabefelder zugeklappt (nur Kopfzeile); die
/// Liste hat Vorrang, der Benutzer kann jederzeit aufklappen.
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
    /// zu klein, gilt die Mindesthoehe (siehe <see cref="Berechne"/> fuer das Zuklappen).
    /// </summary>
    public static double DrawerHeight(double gesamtHoehe, double zeilenHoehe, double kopfHoehe, double? gespeichert)
        => Berechne(gesamtHoehe, zeilenHoehe, kopfHoehe, gespeichert).Hoehe;

    /// <summary>
    /// Vollstaendige Regel: Hoehe plus Entscheid, ob die Eingabefelder wegen Platzmangel
    /// zugeklappt werden. Zugeklappt traegt die Hoehe die Mindesthoehe fuer ein spaeteres Oeffnen.
    /// </summary>
    public static DataPageDrawerLayout Berechne(double gesamtHoehe, double zeilenHoehe, double kopfHoehe, double? gespeichert)
    {
        var fuerListe = kopfHoehe + MindestZeilen * zeilenHoehe;
        var maxDrawer = gesamtHoehe - fuerListe - SplitterHoehe;
        if (maxDrawer < MinDrawer)
            return new DataPageDrawerLayout(MinDrawer, Zugeklappt: true);
        var wunsch = gespeichert is > 0 ? gespeichert.Value : Math.Round(gesamtHoehe * Anteil);
        return new DataPageDrawerLayout(Math.Clamp(wunsch, MinDrawer, maxDrawer), Zugeklappt: false);
    }
}
