using System;
using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Fixwelle 2b (P2): Setzt Zeilenhoehe und Mindesthoehe der Haltungstabelle.
///
/// Warum eine eigene Klasse: Die Teildateien der Seite <c>DataPage</c> stehen zusammen unter
/// einer Zeilengrenze (Waechter <c>MaintainabilityFitnessTests</c>). Neue Logik gehoert
/// deshalb daneben, nicht hinein.
///
/// Die Tabelle traegt neben der Zeilenhoehe eine frei einstellbare Mindesthoehe
/// (Ansicht -> Zeilenhoehe, Werkseinstellung 38). Sie war groesser als die kompakte
/// Zeilenhoehe und hat das Token <c>RowHeightCompact</c> vollstaendig ausgehebelt: Gemessen
/// blieb die Zeile bei 38 px, egal welcher Wert im Token stand. Die Entscheidung darueber
/// liegt WPF-frei in <see cref="DataPageZeilenhoehePolicy.Mindesthoehe"/>; hier steht nur das
/// Anwenden.
/// </summary>
internal static class DataPageZeilenhoehenAnwender
{
    /// <param name="einzeilig">Gilt die kompakte Zeilenhoehe fuer die aktuelle Ansicht?</param>
    /// <param name="kompakt">Der Wert des Tokens <c>RowHeightCompact</c>.</param>
    /// <param name="eingestellt">Die vom Benutzer eingestellte Mindesthoehe.</param>
    public static void WendeAn(DataGrid grid, bool einzeilig, double kompakt, double eingestellt)
    {
        ArgumentNullException.ThrowIfNull(grid);

        grid.RowHeight = einzeilig ? kompakt : double.NaN;

        // SetCurrentValue statt Zuweisung: Die Bindung an GridMinRowHeight bleibt bestehen und
        // greift beim naechsten Verstellen des Reglers sofort wieder.
        grid.SetCurrentValue(
            DataGrid.MinRowHeightProperty,
            DataPageZeilenhoehePolicy.Mindesthoehe(einzeilig, kompakt, eingestellt));
    }
}
