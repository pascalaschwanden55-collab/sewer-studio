using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Das Fenster «Verteilen» wird wirklich gezeichnet: Vorschau mit Status-Chips, Filmspalte
/// nur bei Haltungen, Hauptknopf mit der Zahl der Ablagen — im eigenen Prozess mit echtem WPF.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class VerteilenWindowIsolatedSmokeTests
{
    [Fact]
    public async Task Verteilen_fenster_zeigt_vorschau_mit_chips()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(typeof(VerteilenWindowIsolatedSmokeTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            // Wie im Programm: Die Vorschau kehrt auf den Oberflaechen-Thread zurueck.
            SynchronizationContext.SetSynchronizationContext(
                new System.Windows.Threading.DispatcherSynchronizationContext(app.Dispatcher));
            foreach (var resource in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });

            var vorgabe = new VerteilenVorgabe(VerteilArt.Haltungen, DistributionVariant.Normal, @"C:\Filme", null,
                _ => new VerteilZiel(@"C:\Ziel", "Projektordner \\ Haltungen_Verteilt", null));
            using var vm = new VerteilenViewModel(vorgabe, new VorschauFake(), new KeineDialoge());
            var window = new VerteilenWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
            };
            WindowFx.SetEntrance(window, false);
            window.Show();
            vm.Quelle = VerteilQuelle.PdfOrdner(@"C:\Quelle");
            Warte(window, vm);

            var host = (FrameworkElement)window.Content;
            var texte = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<TextBlock>(host)
                .Select(t => t.Text).ToList();
            Assert.True(texte.Contains("gefunden"), string.Join(" | ", texte));
            Assert.Contains("mehrdeutig", texte);
            Assert.Contains("nicht zugeordnet", texte);
            Assert.Contains("Jetzt verteilen (2 Dateien)", texte);
            Assert.Contains("3 Dateien · 2 werden abgelegt · 2 brauchen Aufmerksamkeit", texte);
            var grid = Assert.Single(AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<DataGrid>(host));
            var film = grid.Columns.Single(c => Equals(c.Header, "Film"));
            Assert.Equal(Visibility.Visible, film.Visibility);

            vm.IstSchaechte = true;
            Warte(window, vm);
            Assert.Equal(Visibility.Collapsed, film.Visibility);

            vm.AbbrechenCommand.Execute(null);
            Assert.False(window.IsVisible);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    private static void Warte(Window window, VerteilenViewModel vm)
    {
        var bis = DateTime.UtcNow.AddSeconds(10);
        while ((!vm.LaufendeVorschau.IsCompleted || vm.IstBerechnung) && DateTime.UtcNow < bis)
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.True(vm.LaufendeVorschau.IsCompleted, "Vorschau wurde nicht fertig.");
    }

    private sealed class VorschauFake : IVerteilVorschau
    {
        public VerteilVorschauErgebnis Plane(
            VerteilVorschauAnfrage anfrage, IProgress<VerteilVorschauFortschritt>? fortschritt, CancellationToken abbruch)
            => new(3,
            [
                new VerteilVorschauZeile("a.pdf", "1-2", "1-2", "20260712_1-2.mp4", VerteilVorschauStatus.FilmGefunden, ""),
                new VerteilVorschauZeile("b.pdf", "3-4", "3-4", "2 Filme – bitte prüfen", VerteilVorschauStatus.FilmMehrdeutig, ""),
                new VerteilVorschauZeile("c.pdf", null, null, null, VerteilVorschauStatus.NichtZugeordnet, "nicht erkannt"),
            ], []);
    }

    private sealed class KeineDialoge : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
