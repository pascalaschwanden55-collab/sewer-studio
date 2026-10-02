using AuswertungPro.Next.Application.UseCases.Datenaenderungen;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Gemeinsamer Abschluss einer Uebernahme, die Feldwerte an der Eingabe vorbei schreibt
/// (QGIS-Nachfuellen, GeoShop-Abgleich, WebGIS-Holen), fuer die Haltungs- UND die Schachtseite
/// (Deepscan 02.10.2026, A2). Vorher stand der Abschluss je Seite und Weg einzeln, und die Wege
/// liefen auseinander: Schaechte-QGIS zeichnete das Formular nicht neu, QGIS auf beiden Seiten
/// markierte das Projekt nicht als geaendert (keine Titelmarke, keine Rueckfrage beim Schliessen,
/// kein Autosave), obwohl die Werte schon im Datensatz standen.
///
/// Reihenfolge: Projekt geaendert, Autosave nach Einstellung, Rueckgaengig-Verlauf leeren
/// (<see cref="IDatenaenderungsVerlauf"/>-Regel fuer externe Schreibwege), dann das Ereignis, an dem
/// die Seite das offene Formular neu zeichnet. Beide Seiten rufen nur
/// <c>MeldeUebernahme()</c>, und das ruft nur dies hier.
/// </summary>
internal static class SeitenUebernahme
{
    public static void Abschliessen(ShellViewModel shell, Action planeAutoSave, Action? felderExternErgaenzt)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(planeAutoSave);

        shell.MarkProjectDirty();
        planeAutoSave();
        shell.DatenVerlauf.Leere(DatenaenderungsVerlauf.GrundUebernahme);
        felderExternErgaenzt?.Invoke();
    }
}
