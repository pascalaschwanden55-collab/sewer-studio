using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class ProjektPruefungUiTests
{
    [Fact]
    public async Task Pruefliste_rendert_hell_und_dunkel_und_Befundsprung_markiert_die_richtige_Zeile()
    {
        var r = await WpfIsolatedTestProcess.RunAsync(typeof(ProjektPruefungUiTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(45));
        Assert.False(r.TimedOut, r.DescribeFailure());
        Assert.True(r.ExitCode == 0 && r.ChildScenarioCompleted, r.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", TestAblage("settings"));
            // Echte Ressourcen, aber kein App.OnStartup beim Pumpen des Dispatchers.
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });
            app.Resources["VsaCodeToLabel"] = new Views.Windows.VsaCodeToLabelConverter();
            var beobachtet = new Project(); var datensatz = new HaltungRecord(); beobachtet.Data.Add(datensatz);
            var meldungen = 0;
            using (var beobachter = new ProjektDatensatzBeobachter(beobachtet, () => meldungen++))
            {
                for (var i = 0; i < 50; i++) datensatz.SetFieldValue(FieldKeys.HoldingLengthMeters, i.ToString(), FieldSource.Manual, true);
                WpfBindungsPumpe.Leeren(); Assert.Equal(1, meldungen);
                datensatz.BearbeitungErledigt = true; beobachter.Dispose();
                WpfBindungsPumpe.Leeren(); Assert.Equal(1, meldungen);
            }
            var p = new Project();
            using var vm = new ProjektPruefungViewModel(new ProjektPruefungService(), () => (p, null),
                new JsonProjectRepository().DeepCopy, new JsonProjectContentSignature().Compute, () => true, _ => { });
            vm.IstAktuell = true;
            vm.Meldung = "5 Hinweise · 12 Haltungen und 4 Schächte geprüft · Stand 10:42.";
            vm.Punkte = new[]
            {
                new ProjektPruefpunkt(ProjektPruefbereich.Dateien, "haltung", Guid.NewGuid(), "10001-10002 – Hauptleitung West mit langem Namen", "Videos/10001-10002.mp4: Datei fehlt."),
                new ProjektPruefpunkt(ProjektPruefbereich.KiBefunde, "haltung", Guid.NewGuid(), "10001-10002", "BAB: KI-Vorschlag noch nicht bestätigt."),
                new ProjektPruefpunkt(ProjektPruefbereich.Meterangaben, "haltung", Guid.NewGuid(), "10003-10004", "BAB bei 24 m liegt hinter der Haltungslänge 20 m (Toleranz 1 m)."),
                new ProjektPruefpunkt(ProjektPruefbereich.Schachthoehen, "schacht", Guid.NewGuid(), "10005", "Deckelhöhe, Sohlenhöhe und Tiefe passen nicht zusammen."),
                new ProjektPruefpunkt(ProjektPruefbereich.Eingabefelder, "haltung", Guid.NewGuid(), "10006-10007", "Bauwerksteil: Bezeichnung darf nicht leer sein.")
            };
            var view = new ProjektPruefungView { DataContext = vm, Margin = new Thickness(24) };
            // Wie die echte Projektübersicht: ein äußerer Seiten-ScrollViewer. Die Prüfliste
            // bindet ihre Höhe an dessen Viewport und behält deshalb ihre eigene Virtualisierung.
            var seitenScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = view
            };
            var window = new Window { Content = seitenScroll, Height = 650, Width = 1100, ShowActivated = false,
                ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
            seitenScroll.SetResourceReference(Control.BackgroundProperty, "BgBrush");
            window.Show();
            foreach (var theme in new[] { "Light", "Dark" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri("/SewerStudio;component/Theme/" + (theme == "Light" ? "ThemeLight.xaml" : "Theme.xaml"), UriKind.Relative) };
                // Das ist kein Wechsel der Monitor-DPI. Die Breiten bilden nur nach, wie viel
                // Platz bei 100/125/150/200 % Skalierung in DIPs effektiv uebrig bleibt.
                foreach (var (skalierung, width) in new[] { (100, 1100d), (125, 880d), (150, 733d), (200, 550d) })
                {
                    window.Width = width; window.UpdateLayout(); WpfBindungsPumpe.Leeren();
                    var grid = Assert.Single(VisualTreeSafe.FindDescendants<DataGrid>(view));
                    Assert.Equal(5, grid.Items.Count); Assert.True(grid.IsVisible);
                    Assert.Same(app.FindResource("HeaderBrush"), grid.AlternatingRowBackground);
                    Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
                    Assert.True(VirtualizingPanel.GetIsVirtualizing(grid));
                    Assert.Equal(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(grid));
                    Assert.All(grid.Columns.Take(3), column => Assert.Equal(DataGridLengthUnitType.Star, column.Width.UnitType));
                    Assert.True(grid.Columns[1].Width.Value > grid.Columns[0].Width.Value,
                        "Der Objektname erhält im schmalen Layout mehr Platz als der Bereich.");
                    Assert.Equal(DataGridLengthUnitType.Auto, grid.Columns[3].Width.UnitType);
                    Assert.True(grid.MaxHeight > 0);
                    Assert.Equal(seitenScroll.ViewportHeight, grid.MaxHeight, 3);
                    Assert.Equal(ScrollBarVisibility.Disabled, seitenScroll.HorizontalScrollBarVisibility);
                    Assert.True(view.ActualWidth > seitenScroll.ViewportWidth * .75,
                        "Die Karte muss im Seiten-Viewport die verfügbare Breite nutzen.");
                    var buttons = VisualTreeSafe.FindDescendants<Button>(view).ToArray();
                    var pruefen = Assert.Single(buttons.Where(b => Equals(b.Content, "Projekt prüfen") && b.Command == vm.PruefenCommand));
                    var abbrechen = Assert.Single(buttons.Where(b => Equals(b.Content, "Abbrechen") && b.Command == vm.AbbrechenCommand));
                    Assert.True(Kontrast(pruefen) >= 4.5,
                        "Der aktive Primärknopf braucht zwischen Schrift und Hintergrund mindestens 4,5:1 Kontrast.");
                    Assert.Equal("Projektprüfung starten", AutomationProperties.GetName(pruefen));
                    Assert.Equal("Projektprüfung abbrechen", AutomationProperties.GetName(abbrechen));
                    Assert.Equal(5, buttons.Count(b => Equals(b.Content, "Zur Stelle") && b.Command == vm.OeffnenCommand));
                    Assert.All(buttons.Where(b => Equals(b.Content, "Zur Stelle")), b => Assert.Equal("Hinweis öffnen", AutomationProperties.GetName(b)));
                    Assert.All(buttons.Where(b => Equals(b.Content, "Zur Stelle")), b => Assert.Equal(VerticalAlignment.Top, b.VerticalAlignment));
                    Assert.Equal("Hinweise der Projektprüfung", AutomationProperties.GetName(grid));
                    Assert.Equal(2, grid.TabIndex);
                    Assert.True(grid.IsTabStop);
                    Assert.True(pruefen.IsTabStop); Assert.Equal(0, pruefen.TabIndex);
                    Assert.True(abbrechen.IsTabStop); Assert.Equal(1, abbrechen.TabIndex);
                    var status = Assert.Single(VisualTreeSafe.FindDescendants<TextBlock>(view)
                        .Where(t => AutomationProperties.GetName(t) == "Status der Projektprüfung"));
                    Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
                    var start = Assert.Single(view.InputBindings.OfType<KeyBinding>().Where(b => b.Key == Key.P && b.Modifiers == ModifierKeys.Alt));
                    var stop = Assert.Single(view.InputBindings.OfType<KeyBinding>().Where(b => b.Key == Key.Escape));
                    var open = Assert.Single(grid.InputBindings.OfType<KeyBinding>().Where(b => b.Key == Key.Enter));
                    Assert.Same(vm.PruefenCommand, start.Command); Assert.Same(vm.AbbrechenCommand, stop.Command); Assert.Same(vm.OeffnenCommand, open.Command);
                    Assert.NotNull(view.Resources["ProjektPruefungCard"]);
                    Assert.NotNull(view.Resources["ProjektPruefungPrimaryButton"]);
                    Assert.NotNull(view.Resources["ProjektPruefungGrid"]);
                    vm.FokusPunkt = vm.Punkte[3]; WpfBindungsPumpe.Leeren(); window.UpdateLayout();
                    Assert.Same(vm.Punkte[3], grid.SelectedItem);
                    Assert.True(grid.IsKeyboardFocusWithin);
                    Assert.Null(vm.FokusPunkt);
                    foreach (var row in VisualTreeSafe.FindDescendants<DataGridRow>(grid))
                    foreach (var text in VisualTreeSafe.FindDescendants<TextBlock>(row).Where(t => t.TextWrapping == TextWrapping.Wrap))
                        Assert.True(text.DesiredSize.Height <= row.ActualHeight, "Der Hinweis muss vollständig in die Zeile passen.");
                    Zeichne(seitenScroll, $"projektpruefung-{theme}-{skalierung}");
                }
            }
            vm.Punkte = Enumerable.Range(0, 600).Select(i => new ProjektPruefpunkt(
                ProjektPruefbereich.Dateien, "haltung", Guid.NewGuid(), $"Haltung {i}", $"Hinweis {i}")).ToArray();
            window.UpdateLayout(); WpfBindungsPumpe.Leeren();
            var virtualisierteListe = Assert.Single(VisualTreeSafe.FindDescendants<DataGrid>(view));
            Assert.True(VisualTreeSafe.FindDescendants<DataGridRow>(virtualisierteListe).Count() < vm.Punkte.Count,
                "Die Ergebnisliste darf im äußeren Seiten-ScrollViewer nicht alle Befunde erzeugen.");
            vm.Punkte = []; window.UpdateLayout(); WpfBindungsPumpe.Leeren();
            Assert.False(VisualTreeSafe.FindDescendants<DataGrid>(view).Single().IsVisible);
            window.Close();

            var erster = new ProtocolEntry { Code = "BAB" }; var ziel = new ProtocolEntry { Code = "BAF" };
            var h = new HaltungRecord { Protocol = new() { Current = new() { Entries = [erster, ziel] } } }; p.Data.Add(h);
            using var logger = LoggerFactory.Create(_ => { });
            var sp = new ServiceProvider(new(), new DiagnosticsOptions(), logger.CreateLogger("test"), logger);
            var dialog = new Views.ProtocolObservationsWindow(h, p, sp, null, null, () => { })
            { ShowInTaskbar = false, ShowActivated = false, WindowState = WindowState.Normal,
                Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            var gridZiel = Assert.IsType<DataGrid>(dialog.FindName("EntriesGrid"));
            WindowFx.SetEntrance(dialog, false);
            ProjektPruefpunktNavigation.MarkiereEintrag(dialog, ziel.EntryId);
            dialog.Show(); dialog.UpdateLayout();
            Assert.Same(ziel, gridZiel.SelectedItem);
            dialog.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted(); app.Shutdown();
        });
    }

    private static void Zeichne(FrameworkElement host, string name)
    {
        var bitmap = new RenderTargetBitmap((int)host.ActualWidth, (int)host.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        var ordner = TestAblage("renders");
        Directory.CreateDirectory(ordner);
        using var output = File.Create(Path.Combine(ordner, name + ".png")); png.Save(output);
    }

    private static string TestAblage(string teil)
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "SewerStudio", "Tests", "ProjektPruefung", teil);

    private static double Kontrast(Control control)
    {
        var hintergrund = Assert.IsType<SolidColorBrush>(control.Background).Color;
        var vordergrund = Assert.IsType<SolidColorBrush>(control.Foreground).Color;
        var hell = Math.Max(Luminanz(hintergrund), Luminanz(vordergrund));
        var dunkel = Math.Min(Luminanz(hintergrund), Luminanz(vordergrund));
        return (hell + .05) / (dunkel + .05);
    }

    private static double Luminanz(Color farbe)
    {
        static double Linear(byte kanal)
        {
            var sRgb = kanal / 255d;
            return sRgb <= .04045 ? sRgb / 12.92 : Math.Pow((sRgb + .055) / 1.055, 2.4);
        }

        return .2126 * Linear(farbe.R) + .7152 * Linear(farbe.G) + .0722 * Linear(farbe.B);
    }
}
