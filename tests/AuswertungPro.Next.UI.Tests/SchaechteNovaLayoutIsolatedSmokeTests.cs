using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2: Die Schaechte-Seite baut sich mit den echten App-Ressourcen auf, und
/// zugeklappte Eingabefelder geben der Liste die Flaeche zurueck. Kopie von
/// <see cref="DataPageNovaLayoutIsolatedSmokeTests"/> mit getauschten Typen (SchaechtePage,
/// Elemente DrawerRow, DrawerSplitterRow, FelderDrawer). Laeuft wie die anderen WPF-Smoke-Tests
/// in einem eigenen Kindprozess; kein Projekt, kein ViewModel, kein Fensterstart.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class SchaechteNovaLayoutIsolatedSmokeTests
{
    private static readonly string ChildTestName =
        typeof(SchaechteNovaLayoutIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_baut_Schaechteseite_und_zugeklappte_Eingabefelder_geben_Platz_frei);

    [Fact]
    public async Task Schaechteseite_laesst_sich_in_eigenem_Wpf_Prozess_aufbauen()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(ChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_baut_Schaechteseite_und_zugeklappte_Eingabefelder_geben_Platz_frei()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            Assert.NotNull(app.TryFindResource("CompactToggleButton"));

            var page = new Views.Pages.SchaechtePage();
            Layout(page);

            // Nova, Aufklapp-Liste (Task 6): Standard ist die Liste; die Eingabefelder-Schublade
            // gehoert erst zur Tabelle.
            PruefeStandardIstDieAufklappListe(page);
            WechsleAufDieTabelle(page);
            PruefeSucheUndFilter(page);

            var drawer = Assert.IsType<HaltungFelderDrawer>(page.FindName("FelderDrawer"));
            var drawerRow = Assert.IsType<RowDefinition>(page.FindName("DrawerRow"));
            var splitterRow = Assert.IsType<RowDefinition>(page.FindName("DrawerSplitterRow"));

            // Aufgeklappt seit dem Wechsel auf die Tabelle (ApplyDrawerOpenState laeuft dabei
            // ueber SchaechteNovaWorkspaceController.SetzeSichtbar(uebersicht, eingabefelder)).
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

            // Wie bei den Haltungen: "Gross anzeigen" stellt die Themen in zwei Spalten statt in
            // einer Zeile dar (derselbe HaltungFelderDrawer wird hier wiederverwendet).
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
                var themedPage = new Views.Pages.SchaechtePage();
                // Die Schaechte-Seite besitzt kein eigenes "PageCompactToggleButton" wie DataPage;
                // ihre Spaltenansicht-Chips verwenden direkt das globale CompactToggleButton.
                var chip = new ToggleButton
                {
                    Content = "Kompakt", IsChecked = true,
                    Style = (Style)themedPage.FindResource("CompactToggleButton")
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
                NovaRenderingChecks.LongMenuCanScrollToItsLastAction();
            }

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static readonly string PanelChildTestName =
        typeof(SchaechteNovaLayoutIsolatedSmokeTests).FullName
        + "."
        + nameof(Kindprozess_Schachtuebersicht_folgt_Datensatzaenderungen_ohne_neue_Bindung);

    [Fact]
    public async Task Schachtuebersicht_folgt_Datensatzaenderungen_in_eigenem_Wpf_Prozess()
    {
        Assert.Null(System.Windows.Application.Current);
        var result = await WpfIsolatedTestProcess.RunAsync(PanelChildTestName, TimeSpan.FromSeconds(60));

        Assert.Null(System.Windows.Application.Current);
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    /// <summary>
    /// Fix-Runde 1/2: Das Panel bleibt an <c>Record</c> gebunden (keine neue Bindung, kein
    /// Auswahlwechsel) und muss trotzdem zwei Faelle nachziehen, beide allein ueber
    /// <c>SchachtRecord.PropertyChanged</c>: eine reine Feldaenderung (Grundriss folgt der
    /// Schachtform) und ein In-Place-Ersatz des Protokolls (der <c>Protocol</c>-Setter meldet
    /// sich seit Fix-Runde 2 selbst mit <c>nameof(Protocol)</c>). Der Test ruft
    /// <c>Aktualisiere()</c> bewusst NICHT direkt auf, damit die echte Meldekette geprueft wird.
    /// </summary>
    [IsolatedWpfFact]
    public void Kindprozess_Schachtuebersicht_folgt_Datensatzaenderungen_ohne_neue_Bindung()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            // Task 6: Ohne gewaehlten Schacht steht nur der Leerzustand da - keine Schachtgrafik,
            // keine leeren Beschriftungen, kein Protokollknopf.
            var leeresPanel = new SchachtUebersichtPanel();
            Layout(leeresPanel);
            NovaRenderingChecks.OhneAuswahlNurLeerzustand(leeresPanel);
            var leereGrafik = Assert.IsType<SchachtgrafikControl>(leeresPanel.FindName("Grafik"));
            leereGrafik.ZeichneJetzt();
            Assert.Equal(0, leereGrafik.SymbolAnzahl);

            var record = new SchachtRecord();
            var panel = new SchachtUebersichtPanel { Record = record };
            Layout(panel);

            // Task 5: Die Schachtgrafik (senkrechter Schnitt) ersetzt den frueheren Grundriss-Kreis.
            var grafik = Assert.IsType<SchachtgrafikControl>(panel.FindName("Grafik"));
            grafik.ZeichneJetzt();
            Assert.Equal(0, grafik.SymbolAnzahl);

            // In-Place-Neuaufbau (z. B. "Aktualisieren"): Protocol wird ersetzt. SchachtRecord.Protocol
            // meldet sich seit Fix-Runde 2 selbst (PropertyChanged(nameof(Protocol))) - das Panel zieht
            // die neue Schadensliste allein ueber diese Meldung nach, ohne Aktualisiere() direkt zu rufen.
            // Dieselbe Meldung erreicht auch die Schachtgrafik (sie abonniert sich selbst).
            Assert.Null(panel.Entries);
            record.Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry { Code = "BAB" },
                        new ProtocolEntry { Code = "BAC" },
                        new ProtocolEntry { Code = "BBA" },
                    }
                }
            };
            Layout(panel);
            Assert.Equal(3, panel.Entries?.Count);
            grafik.ZeichneJetzt();
            Assert.Equal(3, grafik.SymbolAnzahl);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 4: Die Suche steht als Pille rechts (ohne F3-Marke), und die
    /// Spaltenchips (Filterzeile der Schachtliste) bleiben sichtbar. Gilt fuer die Tabelle —
    /// die Aufklapp-Liste blendet die Chips aus (siehe <see cref="PruefeStandardIstDieAufklappListe"/>).
    /// </summary>
    private static void PruefeSucheUndFilter(Views.Pages.SchaechtePage page)
    {
        var searchBox = Assert.IsType<TextBox>(page.FindName("SearchBox"));
        var pillBorder = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindAncestor<Border>(searchBox);
        Assert.NotNull(pillBorder);
        Assert.Equal(15d, pillBorder!.CornerRadius.TopLeft);

        var columnViewChips = Assert.IsType<ItemsControl>(page.FindName("ColumnViewChips"));
        Assert.Equal(Visibility.Visible, columnViewChips.Visibility);
    }

    /// <summary>
    /// Nova, Aufklapp-Liste (Task 6): Ohne Umschalten zeigt die Schachtseite die Liste, nicht
    /// die Tabelle — genau wie bei den Haltungen (<c>DataPageNovaLayoutIsolatedSmokeTests</c>).
    /// </summary>
    private static void PruefeStandardIstDieAufklappListe(Views.Pages.SchaechtePage page)
    {
        var liste = Assert.IsType<SchachtAufklappListe>(page.FindName("AufklappListe"));
        var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
        var chips = Assert.IsType<ItemsControl>(page.FindName("ColumnViewChips"));
        var drawer = Assert.IsType<HaltungFelderDrawer>(page.FindName("FelderDrawer"));
        var uebersicht = Assert.IsType<SchachtUebersichtPanel>(page.FindName("Uebersicht"));

        Assert.Equal(Visibility.Visible, liste.Visibility);
        Assert.Equal(Visibility.Collapsed, grid.Visibility);
        Assert.Equal(Visibility.Collapsed, chips.Visibility);
        Assert.Equal(Visibility.Collapsed, drawer.Visibility);
        Assert.Equal(Visibility.Visible, uebersicht.Visibility);

        // Das Zeilen-Kontextmenue ist dasselbe wie an der Tabelle — kein zweiter Befehlsweg.
        Assert.NotNull(liste.ZeilenMenue);
        Assert.Same(liste.ZeilenMenue, grid.ContextMenu);

        var listeMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtListeMenu"));
        var tabelleMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtTabelleMenu"));
        Assert.True(listeMenu.IsChecked, "Beim Start muss \"Aufklapp-Liste\" angehakt sein.");
        Assert.False(tabelleMenu.IsChecked);
    }

    /// <summary>
    /// Nova, Aufklapp-Liste (Task 6): Umschalten auf die Tabelle — dieselbe Auswahl (Selected)
    /// ueberlebt den Wechsel, weil beide Ansichten dieselbe Sammlung binden.
    /// </summary>
    private static void WechsleAufDieTabelle(Views.Pages.SchaechtePage page)
    {
        var tabelleMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtTabelleMenu"));
        var listeMenu = Assert.IsType<MenuItem>(page.FindName("AnsichtListeMenu"));

        tabelleMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Layout(page);

        var liste = Assert.IsType<SchachtAufklappListe>(page.FindName("AufklappListe"));
        var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
        var chips = Assert.IsType<ItemsControl>(page.FindName("ColumnViewChips"));
        Assert.Equal(Visibility.Collapsed, liste.Visibility);
        Assert.Equal(Visibility.Visible, grid.Visibility);
        Assert.Equal(Visibility.Visible, chips.Visibility);
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
