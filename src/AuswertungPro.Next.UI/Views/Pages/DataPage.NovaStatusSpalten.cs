using System;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;
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

    /// <summary>Traegt diese Spalte im Nova-Layout die Zustandsklassen-Marke statt einer Farbzelle?</summary>
    private bool IstNovaZustandsklasseSpalte(string field)
        => NovaLayoutAktiv && string.Equals(field, FieldKeys.ConditionClass, StringComparison.Ordinal);

    /// <summary>Spaltenkoepfe schreiben gross; dieselbe Regel wie in <see cref="DataPageColumnFactory"/>.</summary>
    private static string Grossschrift(string header)
        => GrossbuchstabenConverter.Anwenden(header) ?? header;

    /// <summary>
    /// Haengt KI, Pruefung, Video und Protokoll ans Ende der Tabelle. Sie werden wie jede
    /// andere Spalte in <c>_columnFields</c> gefuehrt, damit der Spaltenansicht-Umschalter sie
    /// ein- und ausblenden kann — gespeichert wird ihr Layout jedoch nie
    /// (<see cref="NovaStatusSpalten"/>, <see cref="DataGridColumnLayoutController"/>).
    /// </summary>
    private void ErgaenzeNovaStatusSpalten()
    {
        if (!NovaLayoutAktiv)
            return;

        Ergaenze(NovaStatusSpalten.Ki, HaltungStatusColumnFactory.Ki(Grossschrift("KI")));
        Ergaenze(NovaStatusSpalten.Pruefung, HaltungStatusColumnFactory.Pruefung(Grossschrift("Prüfung")));
        Ergaenze(NovaStatusSpalten.Video, HaltungStatusColumnFactory.Video(Grossschrift("Video")));
        Ergaenze(NovaStatusSpalten.Protokoll, HaltungStatusColumnFactory.Protokoll(Grossschrift("Protokoll")));
    }

    private void Ergaenze(string schluessel, DataGridColumn spalte)
    {
        spalte.SetValue(System.Windows.FrameworkElement.TagProperty, schluessel);
        Grid.Columns.Add(spalte);
        _columnFields[spalte] = schluessel;
    }

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
