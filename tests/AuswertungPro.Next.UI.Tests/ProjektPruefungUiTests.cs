using System.IO;
using System.Windows;
using System.Windows.Controls;
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
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", TestRepoPaths.RepoFile(".tmp", "projektpruefung-ui-settings"));
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
                new ProjektPruefpunkt(ProjektPruefbereich.Dateien, "haltung", Guid.NewGuid(), "10001-10002", "Videos/10001-10002.mp4: Datei fehlt."),
                new ProjektPruefpunkt(ProjektPruefbereich.KiBefunde, "haltung", Guid.NewGuid(), "10001-10002", "BAB: KI-Vorschlag noch nicht bestätigt."),
                new ProjektPruefpunkt(ProjektPruefbereich.Meterangaben, "haltung", Guid.NewGuid(), "10003-10004", "BAB bei 24 m liegt hinter der Haltungslänge 20 m (Toleranz 1 m)."),
                new ProjektPruefpunkt(ProjektPruefbereich.Schachthoehen, "schacht", Guid.NewGuid(), "10005", "Deckelhöhe, Sohlenhöhe und Tiefe passen nicht zusammen."),
                new ProjektPruefpunkt(ProjektPruefbereich.Eingabefelder, "haltung", Guid.NewGuid(), "10006-10007", "Bauwerksteil: Bezeichnung darf nicht leer sein.")
            };
            var view = new ProjektPruefungView { DataContext = vm, Margin = new Thickness(24) };
            var window = new Window { Content = view, Height = 650, Width = 1100, ShowActivated = false,
                ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.SetResourceReference(Window.BackgroundProperty, "BgBrush");
            window.Show();
            foreach (var theme in new[] { "Light", "Dark" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri("/SewerStudio;component/Theme/" + (theme == "Light" ? "ThemeLight.xaml" : "Theme.xaml"), UriKind.Relative) };
                foreach (var width in new[] { 1100, 800 })
                {
                    window.Width = width; window.UpdateLayout(); WpfBindungsPumpe.Leeren();
                    var grid = Assert.Single(VisualTreeSafe.FindDescendants<DataGrid>(view));
                    Assert.Equal(5, grid.Items.Count); Assert.True(grid.IsVisible);
                    Assert.Same(app.FindResource("HeaderBrush"), grid.AlternatingRowBackground);
                    var buttons = VisualTreeSafe.FindDescendants<Button>(view).ToArray();
                    Assert.Contains(buttons, b => Equals(b.Content, "Projekt prüfen") && b.Command == vm.PruefenCommand);
                    Assert.Equal(5, buttons.Count(b => Equals(b.Content, "Zur Stelle") && b.Command == vm.OeffnenCommand));
                    foreach (var row in VisualTreeSafe.FindDescendants<DataGridRow>(grid))
                    foreach (var text in VisualTreeSafe.FindDescendants<TextBlock>(row).Where(t => t.TextWrapping == TextWrapping.Wrap))
                        Assert.True(text.DesiredSize.Height <= row.ActualHeight, "Der Hinweis muss vollstaendig in die Zeile passen.");
                    Zeichne(view, window.Background, $"projektpruefung-{theme}-{width}");
                }
            }
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

    private static void Zeichne(FrameworkElement host, Brush hintergrund, string name)
    {
        var bitmap = new RenderTargetBitmap((int)host.ActualWidth, (int)host.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var c = visual.RenderOpen())
        {
            var rect = new Rect(0, 0, host.ActualWidth, host.ActualHeight);
            c.DrawRectangle(hintergrund, null, rect); c.DrawRectangle(new VisualBrush(host), null, rect);
        }
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(TestRepoPaths.RepoFile(".tmp"));
        using var output = File.Create(TestRepoPaths.RepoFile(".tmp", name + ".png")); png.Save(output);
    }
}
