using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>
/// Eingabefelder unter der Haltungsliste: die Themen des RecordDetailsBuilders nebeneinander,
/// jedes einzeln aufklappbar. Die Felder selbst rendert der unveraenderte RecordDetailsView;
/// hier gibt es nur Filterung nach Beschriftung und Auf-/Zuklappen.
/// </summary>
public partial class HaltungFelderDrawer : UserControl
{
    public HaltungFelderDrawer() => InitializeComponent();

    public static readonly DependencyProperty TitelProperty = DependencyProperty.Register(
        nameof(Titel), typeof(string), typeof(HaltungFelderDrawer), new PropertyMetadata(string.Empty));

    /// <summary>Haltungsname in der Kopfzeile.</summary>
    public string Titel
    {
        get => (string)GetValue(TitelProperty);
        set => SetValue(TitelProperty, value);
    }

    public static readonly DependencyProperty HinweisProperty = DependencyProperty.Register(
        nameof(Hinweis), typeof(string), typeof(HaltungFelderDrawer), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Sichtbarer Hinweis in der Kopfzeile, zum Beispiel eine verworfene Eingabe nach einem
    /// Konflikt mit einer neueren Tabellenkorrektur (W01). Leer = kein Hinweis.
    /// </summary>
    public string Hinweis
    {
        get => (string)GetValue(HinweisProperty);
        set => SetValue(HinweisProperty, value);
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(HaltungFelderDrawer),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((HaltungFelderDrawer)d).IsOpenChanged?.Invoke(d, EventArgs.Empty)));

    /// <summary>
    /// Aufgeklappt (Themen sichtbar) oder zugeklappt (nur die Kopfzeile). Die Seite reagiert
    /// darauf und gibt der Liste beim Zuklappen die Flaeche zurueck (Nachpruefung W03).
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Wird bei jedem Wechsel von <see cref="IsOpen"/> ausgeloest.</summary>
    public event EventHandler? IsOpenChanged;

    public static readonly DependencyProperty IsTallProperty = DependencyProperty.Register(
        nameof(IsTall), typeof(bool), typeof(HaltungFelderDrawer),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((HaltungFelderDrawer)d).IsTallChanged?.Invoke(d, EventArgs.Empty)));

    /// <summary>
    /// „Gross anzeigen" (Inventar 4.3/8.6, Spec = Prototyp): die Themen stehen in zwei Spalten
    /// statt in einer Zeile, und die Eingabefelder-Zeile bekommt eine 60-Prozent-Wunschhoehe der
    /// Arbeitsflaeche — geklemmt auf dieselbe Sieben-Zeilen-Regel wie jede andere Hoehe. Die
    /// Groesse wird nicht gespeichert; beim Ausschalten gilt wieder die normale, gespeicherte Hoehe.
    /// </summary>
    public bool IsTall
    {
        get => (bool)GetValue(IsTallProperty);
        set => SetValue(IsTallProperty, value);
    }

    /// <summary>Wird bei jedem Wechsel von <see cref="IsTall"/> ausgeloest.</summary>
    public event EventHandler? IsTallChanged;

    public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register(
        nameof(Groups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(HaltungFelderDrawer),
        new PropertyMetadata(null, (d, _) => ((HaltungFelderDrawer)d).Filtern()));

    /// <summary>Themen der gewaehlten Haltung (aus DataPageRecordDetailsBuilder).</summary>
    public IReadOnlyList<RecordDetailGroup>? Groups
    {
        get => (IReadOnlyList<RecordDetailGroup>?)GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public static readonly DependencyProperty SichtbareGruppenProperty = DependencyProperty.Register(
        nameof(SichtbareGruppen), typeof(IReadOnlyList<ThemaAnzeige>), typeof(HaltungFelderDrawer),
        new PropertyMetadata(null));

    /// <summary>Nach der Feldsuche gefilterte Themen; leere Themen werden ausgeblendet.</summary>
    public IReadOnlyList<ThemaAnzeige>? SichtbareGruppen
    {
        get => (IReadOnlyList<ThemaAnzeige>?)GetValue(SichtbareGruppenProperty);
        private set => SetValue(SichtbareGruppenProperty, value);
    }

    /// <summary>
    /// Reine Filterregel: nur Felder, deren Beschriftung den Suchtext enthaelt. Reihenfolge,
    /// Zaehler und die Regel "Weitere Angaben startet zugeklappt" liegen gemeinsam mit der
    /// Aufklapp-Liste in <see cref="HaltungThemenGruppierung"/> — keine zweite Kopie.
    /// </summary>
    internal static IReadOnlyList<ThemaAnzeige> Filtere(IReadOnlyList<RecordDetailGroup>? gruppen, string? suche)
        => HaltungThemenGruppierung.Filtere(gruppen, suche);

    private void FeldSuche_TextChanged(object sender, TextChangedEventArgs e) => Filtern();

    private void Filtern() => SichtbareGruppen = HaltungThemenGruppierung.Filtere(Groups, FeldSuche?.Text);

    private void AlleAuf_Click(object sender, RoutedEventArgs e) => SetzeAlle(true);

    private void AlleZu_Click(object sender, RoutedEventArgs e) => SetzeAlle(false);

    private void SetzeAlle(bool offen)
    {
        for (var i = 0; i < Themen.Items.Count; i++)
        {
            if (Themen.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp
                && FindExpander(cp) is { } ex)
                ex.IsExpanded = offen;
        }
    }

    private static Expander? FindExpander(DependencyObject d)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is Expander ex) return ex;
            if (FindExpander(c) is { } inner) return inner;
        }
        return null;
    }
}
