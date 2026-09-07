using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.3): Die vier nur lesenden Statusspalten und die
/// Zustandsklassen-Marke haengen an der Haltungstabelle — aber nur im Nova-Layout.
/// Ist <c>AppSettings.ShowHaltungenNovaLayout</c> aus, bleibt die Tabelle genau wie bisher,
/// samt der ganzflaechig eingefaerbten Zustandsklassen-Zelle
/// (<see cref="ZustandsklasseCellStyleFactory"/>).
/// </summary>
public partial class DataPage
{
    /// <summary>
    /// Standard ist das Nova-Layout — auch ohne gebundenes ViewModel (Smoke-Test,
    /// Seitenaufbau vor dem DataContext). Nur eine ausdrueckliche Einstellung schaltet zurueck.
    /// </summary>
    private bool NovaLayoutAktiv
        => (DataContext as DataPageViewModel)?.Settings.ShowHaltungenNovaLayout ?? true;

    /// <summary>
    /// Einzeilige Zeilen in Kompakt, Stammdaten, Sanierung und Kosten; "Alle Spalten" und
    /// "Bewertung" bleiben auf Auto, weil dort lange Texte wie "Primaere Schaeden" stehen.
    /// Ausserhalb des Nova-Layouts wird die Zeilenhoehe nicht angefasst.
    /// </summary>
    private void WendeZeilenhoeheAn(string? ansichtsSchluessel)
    {
        if (Grid is null)
            return;

        Grid.RowHeight = NovaLayoutAktiv && DataPageZeilenhoehePolicy.IstEinzeilig(ansichtsSchluessel)
            ? EinzeiligeZeilenhoehe()
            : double.NaN;
    }

    /// <summary>Zeilenhoehe aus dem Token <c>RowHeightCompact</c>; ohne Ressourcen gilt sein Wert aus Controls.xaml.</summary>
    private double EinzeiligeZeilenhoehe()
        => TryFindResource("RowHeightCompact") is double hoehe && hoehe > 0 ? hoehe : 36d;
}
