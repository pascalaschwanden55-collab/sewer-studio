using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    /// <summary>Ein Thema mit genau einer Gruppe, damit RecordDetailsView es unveraendert rendert.</summary>
    public sealed record ThemaAnzeige(string Title, IReadOnlyList<RecordDetailGroup> EinzelGruppe);

    /// <summary>Reine Filterregel: nur Felder, deren Beschriftung den Suchtext enthaelt.</summary>
    internal static IReadOnlyList<ThemaAnzeige> Filtere(IReadOnlyList<RecordDetailGroup>? gruppen, string? suche)
    {
        var q = (suche ?? string.Empty).Trim();
        return (gruppen ?? Array.Empty<RecordDetailGroup>())
            .Select(g => q.Length == 0
                ? g
                : g with { Items = g.Items.Where(i => i.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList() })
            .Where(g => g.Items.Count > 0)
            .Select(g => new ThemaAnzeige(g.Title, new[] { g }))
            .ToList();
    }

    private void FeldSuche_TextChanged(object sender, TextChangedEventArgs e) => Filtern();

    private void Filtern() => SichtbareGruppen = Filtere(Groups, FeldSuche?.Text);

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
