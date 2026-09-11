using System.IO;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Objektakten;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Das Fenster «Liste bearbeiten» wird wirklich gezeichnet, nimmt einen eigenen Eintrag an
/// und schreibt ihn programmweit - im eigenen Prozess mit echtem WPF.</summary>
[Collection("IsolatedWpf")]
public sealed class ListenErgaenzungWindowIsolatedSmokeTests
{
    [Fact]
    public async Task Liste_bearbeiten_zeichnet_Zeilen_und_speichert_einen_eigenen_Eintrag()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(typeof(ListenErgaenzungWindowIsolatedSmokeTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var ordner = TestRepoPaths.RepoFile(".tmp", "listen-ergaenzung-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ordner);
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", ordner);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });

            var speicher = new ObjektaktenListenErgaenzungenStore(ordner);
            var katalog = FieldCatalog.Objektfelder;
            var material = katalog.Feld("haltung.material");
            var beton = katalog.Auswahl(material.KatalogIdJeEltern)!.Eintraege.Where(e => e.Eltern == "1").ToArray();
            var bearbeitung = new ListenErgaenzungBearbeitung(speicher, "Materialdetail · Materialgruppe: Beton",
                material.KatalogIdJeEltern!, "1", beton);
            var vm = new ListenErgaenzungViewModel(bearbeitung);
            var window = new ListenErgaenzungWindow(vm);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000; window.Top = -20000; window.ShowInTaskbar = false;
            WindowFx.SetEntrance(window, false);
            window.Show();
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            var host = (FrameworkElement)window.Content;
            var zeilen = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<CheckBox>(host)
                .Where(c => Equals(c.Content, "Ausblenden")).ToArray();
            Assert.Equal(14, zeilen.Length); // alle 14 Betonsorten sind gezeichnet, jede mit Ausblenden

            vm.NeuText = "Sichtbeton, alt";
            vm.NeuCode = "";
            Assert.True(vm.HinzufuegenCommand.CanExecute(null));
            vm.HinzufuegenCommand.Execute(null);
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.Equal(15, vm.Zeilen.Count);
            Assert.Contains(AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<TextBlock>(host),
                t => t.Text == "eigener Eintrag");
            Assert.Equal("", vm.Fehler);

            vm.NeuText = "sichtbeton, ALT"; // gleicher Text in anderer Schreibung wird abgewiesen
            vm.HinzufuegenCommand.Execute(null);
            Assert.Contains("steht bereits", vm.Fehler);
            Assert.Equal(15, vm.Zeilen.Count);

            vm.SpeichernCommand.Execute(null); // schliesst das Fenster
            Assert.True(vm.Gespeichert);
            var gelesen = new ObjektaktenListenErgaenzungenStore(ordner).Lade();
            var eigen = Assert.Single(gelesen);
            Assert.Equal(ListenErgaenzungArt.Hinzugefuegt, eigen.Art);
            Assert.Equal("Sichtbeton, alt", eigen.Text);
            Assert.Equal("1", eigen.Eltern);
            Assert.Equal(material.KatalogIdJeEltern, eigen.KatalogId);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
            Directory.Delete(ordner, recursive: true);
        });
    }
}
