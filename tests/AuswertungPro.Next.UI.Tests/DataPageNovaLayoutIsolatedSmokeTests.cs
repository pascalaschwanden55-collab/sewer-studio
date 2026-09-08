using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 1, Nachpruefung W03: Die Haltungen-Seite baut sich mit den echten
/// App-Ressourcen auf, und zugeklappte Eingabefelder geben der Liste die Flaeche zurueck.
/// Laeuft wie die anderen WPF-Smoke-Tests in einem eigenen Kindprozess; kein Projekt,
/// kein ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class DataPageNovaLayoutIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(DataPageNovaLayoutIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_baut_Haltungsseite_und_zugeklappte_Eingabefelder_geben_Platz_frei);

    [Fact]
    public async Task Haltungsseite_laesst_sich_in_eigenem_Wpf_Prozess_aufbauen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_baut_Haltungsseite_und_zugeklappte_Eingabefelder_geben_Platz_frei()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            Assert.NotNull(app.TryFindResource("CompactToggleButton"));

            var page = new Views.Pages.DataPage();
            Layout(page);

            PruefeNovaSpalten(page);
            PruefeStandardIstDieAufklappListe(page);
            WechsleAufDieTabelle(page);
            PruefeNovaSucheUndFilter(page);

            var drawer = Assert.IsType<HaltungFelderDrawer>(page.FindName("FelderDrawer"));
            var drawerRow = Assert.IsType<RowDefinition>(page.FindName("DrawerRow"));
            var splitterRow = Assert.IsType<RowDefinition>(page.FindName("DrawerSplitterRow"));

            Assert.True(drawer.IsOpen);
            var offen = drawerRow.ActualHeight;
            Assert.True(offen >= 120, $"Aufgeklappt: {offen} px");
            Assert.Equal(6, splitterRow.ActualHeight);

            drawer.IsOpen = false;
            Layout(page);
            var zu = drawerRow.ActualHeight;
            Assert.True(zu > 0 && zu < 80, $"Zugeklappt: {zu} px (nur Kopfzeile erwartet)");
            Assert.Equal(0, splitterRow.ActualHeight);

            drawer.IsOpen = true;
            Layout(page);
            Assert.True(drawerRow.ActualHeight >= 120, $"Wieder aufgeklappt: {drawerRow.ActualHeight} px");
            Assert.Equal(6, splitterRow.ActualHeight);

            // Fix-Runde 1 (Inventar 4.3/8.6, Spec = Prototyp): "Gross anzeigen" stellt die Themen
            // in zwei Spalten statt in einer Zeile dar.
            drawer.IsTall = true;
            Layout(page);
            var themen = Assert.IsType<ItemsControl>(drawer.FindName("Themen"));
            var themenPanel = FindDescendant<UniformGrid>(themen);
            Assert.NotNull(themenPanel);
            Assert.Equal(2, themenPanel!.Columns);
            drawer.IsTall = false;
            Layout(page);

            foreach (var theme in new[] { ThemeManager.Dark, ThemeManager.Light })
            {
                var file = theme == ThemeManager.Dark ? "Theme.xaml" : "ThemeLight.xaml";
                app.Resources.MergedDictionaries[0] = new ResourceDictionary
                {
                    Source = new Uri($"/SewerStudio;component/Theme/{file}", UriKind.Relative)
                };
                var themedPage = new Views.Pages.DataPage();
                var chip = new ToggleButton
                {
                    Content = "Kompakt", IsChecked = true,
                    Style = (Style)themedPage.FindResource("PageCompactToggleButton")
                };
                Layout(chip);
                var surface = Assert.IsType<Border>(chip.Template.FindName("SelectionSurface", chip));
                Assert.Equal(ColorOf(app.FindResource("SelectionBackgroundBrush")), ColorOf(surface.Background));
                Assert.Equal(ColorOf(app.FindResource("SelectionTextBrush")), ColorOf(chip.Foreground));
                chip.IsChecked = false;
                Assert.Equal(ColorOf(app.FindResource("CardBrush")), ColorOf(surface.Background));

                // Auch normale, im Code erzeugte Spalten muessen den Theme-Stil erben.
                var cell = new DataGridCell
                {
                    Style = DataGridFieldMetaTooltipStyleFactory.Create("Bemerkungen", null),
                    IsSelected = true
                };
                Layout(cell);
                Assert.Equal(ColorOf(app.FindResource("SelectionBackgroundBrush")), ColorOf(cell.Background));
                Assert.Equal(ColorOf(app.FindResource("SelectionTextBrush")), ColorOf(cell.Foreground));
                NovaRenderingChecks.ColorColumnsUseTheirCellForeground();
                NovaRenderingChecks.SchachtZustandsklasseMarkeBleibtLesbar();
                NovaRenderingChecks.LongMenuCanScrollToItsLastAction();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static readonly string RohrringChildTestName =
        typeof(DataPageNovaLayoutIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Rohrring_und_KI_Hinweis_folgen_derselben_Entries_Instanz);

    [Fact]
    public async Task Rohrring_und_KI_Hinweis_aktualisieren_sich_in_eigenem_Wpf_Prozess()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(RohrringChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    /// <summary>
    /// Fix-Runde 1: DataPageViewModel.SelectedProtocolEntries ist EINE feste
    /// ObservableCollection-Instanz, die beim Haltungswechsel nur geleert und neu gefuellt
    /// wird (kein Property-Wechsel, keine neue Bindungsquelle). RohrringControl und
    /// HaltungUebersichtPanel muessen deshalb auf CollectionChanged der bereits gebundenen
    /// Sammlung reagieren, nicht nur einmal beim ersten Binden rechnen.
    /// </summary>
    [IsolatedWpfFact]
    public void Kindprozess_Rohrring_und_KI_Hinweis_folgen_derselben_Entries_Instanz()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var ringEntries = new ObservableCollection<ProtocolEntry>();
            var ring = new RohrringControl { Entries = ringEntries };
            Layout(ring);
            Assert.Empty(ring.Boegen);

            ringEntries.Add(new ProtocolEntry
            {
                Code = "BAB",
                CodeMeta = new ProtocolEntryCodeMeta { Parameters = { ["Uhr_von"] = "12" } }
            });
            Layout(ring);
            Assert.Single(ring.Boegen);

            ringEntries.Clear();
            Layout(ring);
            Assert.Empty(ring.Boegen);

            // Task 6: Ohne gewaehlte Haltung steht nur der Leerzustand da - kein Rohrring,
            // keine leeren Beschriftungen und vor allem kein "{DependencyProperty.UnsetValue}".
            var leeresPanel = new HaltungUebersichtPanel();
            Layout(leeresPanel);
            NovaRenderingChecks.OhneAuswahlNurLeerzustand(leeresPanel);

            var panelEntries = new ObservableCollection<ProtocolEntry>();
            var panel = new HaltungUebersichtPanel { Entries = panelEntries };
            Layout(panel);
            Assert.Equal(0, panel.OffeneKiBefunde);

            panelEntries.Add(new ProtocolEntry
            {
                Code = "BAB",
                Ai = new ProtocolEntryAiMeta { Accepted = false }
            });
            Layout(panel);
            Assert.Equal(1, panel.OffeneKiBefunde);

            panelEntries.Clear();
            Layout(panel);
            Assert.Equal(0, panel.OffeneKiBefunde);

            // F1: Ring und Liste zeigen nur Schaeden. Bestandsaufnahme (BCD/BCE/BCA/BCC) gehört
            // nicht dazu, und die schwerere Stufe steht vorn.
            panelEntries.Add(Eintrag("BCD", stufe: null));
            panelEntries.Add(Eintrag("BBC", stufe: "1"));
            panelEntries.Add(Eintrag("BAC", stufe: "4"));
            Layout(panel);
            Assert.Equal(new[] { "BAC", "BBC" }, panel.Schaeden.Select(e => e.Code).ToArray());

            // F5: Prüfung und Video hängen an Feldern des Datensatzes. Wird derselbe Datensatz
            // verändert, wechselt die Record-Eigenschaft nicht — das Panel muss trotzdem nachziehen.
            var record = new AuswertungPro.Next.Domain.Models.HaltungRecord();
            panel.Record = record;
            Layout(panel);
            Assert.Equal("kein Video", panel.VideoText);

            record.SetFieldValue(
                AuswertungPro.Next.Domain.Models.FieldKeys.Link,
                @"D:\Medien\10001-10002.mp4",
                AuswertungPro.Next.Domain.Models.FieldSource.Manual,
                false);
            Layout(panel);
            Assert.Equal("10001-10002.mp4", panel.VideoText);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static ProtocolEntry Eintrag(string code, string? stufe)
        => new() { Code = code, Beschreibung = code, CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe } };

    /// <summary>
    /// Nova-Etappe 2b, Task 3: Die fertig aufgebaute Seite fuehrt die vier Statusspalten, die
    /// Zustandsklasse ist eine Marke (Vorlagenspalte statt Textspalte), und "Kompakt" zeigt
    /// genau die zehn Spalten des Prototyps.
    /// </summary>
    private static void PruefeNovaSpalten(Views.Pages.DataPage page)
    {
        // Die Spalten baut der Loaded-Handler der Seite. Ohne Fenster gibt es keine
        // PresentationSource, und WPF loest Loaded dann nie aus — deshalb hier bewusst selbst.
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, page));

        var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
        var felder = grid.Columns.ToDictionary(
            spalte => spalte,
            spalte => spalte.GetValue(FrameworkElement.TagProperty) as string);

        foreach (var schluessel in AuswertungPro.Next.UI.DataPage.NovaStatusSpalten.Alle)
            Assert.Contains(schluessel, felder.Values);

        var zustandsklasse = grid.Columns.Single(
            spalte => (string?)spalte.GetValue(FrameworkElement.TagProperty)
                == AuswertungPro.Next.Domain.Models.FieldKeys.ConditionClass);
        Assert.IsType<DataGridTemplateColumn>(zustandsklasse);

        var ansichten = new AuswertungPro.Next.UI.DataPage.DataPageColumnViewController(
            grid,
            spalte => felder[spalte],
            () => "kompakt",
            _ => { });
        ansichten.Apply("kompakt");

        var sichtbar = grid.Columns
            .Where(spalte => spalte.Visibility == Visibility.Visible)
            .Select(spalte => (string?)spalte.GetValue(FrameworkElement.TagProperty))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var erwartet = AuswertungPro.Next.UI.DataPage.DataPageColumnViewCatalog.Resolve("kompakt").Felder!
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(erwartet, sichtbar);

        ansichten.Apply("alle");
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 4: Ohne DataContext liefert NovaLayoutAktiv true (Standard) — die
    /// Suchpille ist sichtbar, die alte Suche-Zeile ist es nicht, und die Filterzeile
    /// (Spaltenchips und FilterChipBar) bleibt erreichbar.
    ///
    /// Nova-Fixwelle 2b (P2): Die Pille steht jetzt IN der Werkzeugleiste (rechts angedockt),
    /// nicht mehr in einer eigenen Zeile darunter — deshalb ein StackPanel im DockPanel der
    /// Leiste statt einer eigenen Karte.
    /// </summary>
    private static void PruefeNovaSucheUndFilter(Views.Pages.DataPage page)
    {
        var novaSuche = Assert.IsType<StackPanel>(page.FindName("NovaSucheLeiste"));
        Assert.Equal(Visibility.Visible, novaSuche.Visibility);
        Assert.Equal(Dock.Right, DockPanel.GetDock(novaSuche));
        var leiste = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindAncestor<DockPanel>(novaSuche);
        Assert.NotNull(leiste);

        var alteSuche = Assert.IsType<Border>(page.FindName("AlteSucheLeiste"));
        Assert.Equal(Visibility.Collapsed, alteSuche.Visibility);

        var novaSearchBox = Assert.IsType<TextBox>(page.FindName("NovaSearchBox"));
        var pillBorder = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindAncestor<Border>(novaSearchBox);
        Assert.NotNull(pillBorder);
        Assert.Equal(15d, pillBorder!.CornerRadius.TopLeft);

        var columnViewChips = Assert.IsType<ItemsControl>(page.FindName("ColumnViewChips"));
        Assert.Equal(Visibility.Visible, columnViewChips.Visibility);

        var filterChips = Assert.IsType<AuswertungPro.Next.UI.Controls.FilterChipBar>(page.FindName("FilterChips"));
        Assert.Equal(Visibility.Visible, filterChips.Visibility);

        var reihenfolgePopup = Assert.IsType<Popup>(page.FindName("ReihenfolgePopup"));
        Assert.False(reihenfolgePopup.IsOpen);
    }

    /// <summary>
    /// Nova, Aufklapp-Liste (Task 2): Die Seite startet mit der Liste. Die Tabelle, ihre
    /// Spaltenchips und die Eingabefelder-Schublade sind weg; die Uebersicht rechts bleibt.
    /// </summary>
    private static void PruefeStandardIstDieAufklappListe(Views.Pages.DataPage page)
    {
        var liste = Assert.IsType<HaltungAufklappListe>(page.FindName("AufklappListe"));
        var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
        var chips = Assert.IsType<ItemsControl>(page.FindName("ColumnViewChips"));
        var drawer = Assert.IsType<HaltungFelderDrawer>(page.FindName("FelderDrawer"));
        var uebersicht = Assert.IsType<HaltungUebersichtPanel>(page.FindName("Uebersicht"));
        var drawerRow = Assert.IsType<RowDefinition>(page.FindName("DrawerRow"));

        Assert.Equal(Visibility.Visible, liste.Visibility);
        Assert.Equal(Visibility.Collapsed, grid.Visibility);
        Assert.Equal(Visibility.Collapsed, chips.Visibility);
        Assert.Equal(Visibility.Collapsed, drawer.Visibility);
        Assert.Equal(Visibility.Visible, uebersicht.Visibility);
        Assert.Equal(0, drawerRow.ActualHeight);

        // Das Zeilen-Kontextmenue ist dasselbe wie an der Tabelle — kein zweiter Befehlsweg.
        Assert.NotNull(liste.ZeilenMenue);
        Assert.Same(grid.ContextMenu, liste.ZeilenMenue);
    }

    /// <summary>
    /// Der Menuepunkt "Tabelle" schaltet um. Geklickt wird der echte Punkt, damit auch die
    /// Verdrahtung im XAML geprueft ist.
    /// </summary>
    private static void WechsleAufDieTabelle(Views.Pages.DataPage page)
    {
        var tabelleMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtTabelleMenu"));
        var listeMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtListeMenu"));
        Assert.True(listeMenu.IsChecked, "Beim Start muss \"Aufklapp-Liste\" angehakt sein.");

        tabelleMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Layout(page);

        var liste = Assert.IsType<HaltungAufklappListe>(page.FindName("AufklappListe"));
        var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
        Assert.Equal(Visibility.Collapsed, liste.Visibility);
        Assert.Equal(Visibility.Visible, grid.Visibility);
        Assert.True(tabelleMenu.IsChecked);
        Assert.False(listeMenu.IsChecked);
    }

    private static Color ColorOf(object brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
                return typed;
            if (FindDescendant<T>(child) is { } nested)
                return nested;
        }
        return null;
    }

    private static void Layout(UIElement element)
    {
        element.Measure(new Size(1100, 650));
        element.Arrange(new Rect(0, 0, 1100, 650));
        element.UpdateLayout();
    }
}
