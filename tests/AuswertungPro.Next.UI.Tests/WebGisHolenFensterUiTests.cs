using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wunsch Pascal 23.09.2026: Gesperrtes und nicht Zugeordnetes steht im Holen-Fenster sichtbar
/// in einer roten Liste unter einem orangen Band — nicht in einer zugeklappten Hinweisliste.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class WebGisHolenFensterUiTests
{
    [Fact]
    public async Task Gesperrte_und_nicht_zugeordnete_stehen_sichtbar_in_der_roten_liste()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(WebGisHolenFensterUiTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    private static WebGisImportPlan Plan(bool mitSperren)
    {
        var plan = new WebGisImportPlan();
        var gefunden = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "80638-80631", RecordId = Guid.NewGuid() };
        gefunden.Aenderungen.Add(new WebGisImportAenderung
        {
            Feld = WebGisImportPlanBuilder.FeldWebGisGlobalId, Neu = "31946755-E44D-4714-8DA6-91F56A6AC8FA", Grund = "x",
        });
        if (mitSperren)
            gefunden.Hinweise.Add("Rohrmaterial: «Schleuderbeton (SBR)» passt zu keinem SewerStudio-Wert — nicht übernommen.");
        plan.Positionen.Add(gefunden);
        if (mitSperren)
        {
            var gesperrt = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "81156-81157", RecordId = Guid.NewGuid() };
            gesperrt.Sperren.Add("Im WebGIS nicht eindeutig gefunden (kein oder mehrdeutiger Treffer).");
            plan.Positionen.Add(gesperrt);
        }
        return plan;
    }

    private static (WebGisHolenWindow Fenster, Border Host) Zeige(WebGisImportPlan plan)
    {
        var fenster = new WebGisHolenWindow(() => Task.FromResult<WebGisImportPlan?>(plan), _ => "", (_, _) => { });
        fenster.PruefeAsync().GetAwaiter().GetResult(); // der Plan liegt schon vor: laeuft synchron durch
        var inhalt = (FrameworkElement)fenster.Content;
        fenster.Content = null;
        // Der Inhalt erbt seinen DataContext vom Fenster; beim Umhaengen muss er mit.
        var host = new Border { Background = fenster.Background, DataContext = fenster.DataContext, Child = inhalt };
        // NIE die Dispatcher-Warteschlange pumpen: Dann laeuft der geplante App-Start (OnStartup) im
        // Testprozess los und zeigt bei einem Fehler einen echten Meldungsdialog auf dem Bildschirm
        // (23.09.2026 real passiert). Ohne Pumpen bleiben Stern-Spalten im Bild schmal; das Bild ist
        // nur eine Sichtprobe, die Pruefungen haengen nicht daran.
        host.Measure(new Size(1100, 680)); host.Arrange(new Rect(0, 0, 1100, 680)); host.UpdateLayout();
        return (fenster, host);
    }

    private static DataGrid RoteListe(Border host) => VisualTreeSafe.FindDescendants<DataGrid>(host)
        .Single(g => System.Windows.Data.BindingOperations.GetBinding(g, ItemsControl.ItemsSourceProperty)?.Path.Path == "NichtZugeordnet");

    /// <summary>Der rote Rahmen um die Liste (der naechste Vorfahr mit sichtbarem Rand).</summary>
    private static Border Rahmen(DependencyObject element)
    {
        var aktuell = VisualTreeHelper.GetParent(element);
        while (aktuell is not null and not Border { BorderThickness.Left: > 0 }) aktuell = VisualTreeHelper.GetParent(aktuell);
        return (Border)aktuell!;
    }

    /// <summary>Sichtbar heisst: das Element und alle Vorfahren stehen auf Visible (ohne echtes Fenster).</summary>
    private static bool Sichtbar(DependencyObject element)
    {
        for (var e = element; e is not null; e = VisualTreeHelper.GetParent(e))
            if (e is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            // Mit Sperre und nicht zugeordnetem Wert: rote Liste offen, gesperrtes Objekt zuerst, Band warnt.
            var (fenster, host) = Zeige(Plan(mitSperren: true));
            var liste = RoteListe(host);
            Assert.Equal(Visibility.Visible, Rahmen(liste).Visibility);
            var zeilen = liste.Items.Cast<WebGisHolenZeile>().ToList();
            Assert.Equal(2, zeilen.Count);
            Assert.Equal("Haltung 81156-81157", zeilen[0].Objekt);
            Assert.Equal(WebGisImportBericht.ArtObjektGesperrt, zeilen[0].Feld);
            var texte = VisualTreeSafe.FindDescendants<TextBlock>(host).Where(Sichtbar).Select(t => t.Text).ToList();
            Assert.Contains(texte, t => t.StartsWith("Nicht zugeordnet — wird nicht übernommen (2)"));
            Assert.Contains(texte, t => t.Contains("1 Objekt gesperrt") && t.Contains("1 Wert nicht zugeordnet"));
            // Keine zugeklappte Hinweisliste mehr, in der Gesperrtes verschwinden koennte.
            Assert.DoesNotContain(VisualTreeSafe.FindDescendants<Expander>(host), e => !Equals(e.Header, "Ganzer Bericht"));
            if (Environment.GetEnvironmentVariable("SEWER_WEBGIS_HOLEN_BILD") == "1")
            {
                var bild = new RenderTargetBitmap(1100, 680, 96, 96, PixelFormats.Pbgra32);
                bild.Render(host);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bild));
                using var output = File.Create(TestRepoPaths.RepoFile(".tmp", "webgis-holen-gesperrt.png"));
                png.Save(output);
            }
            fenster.Close();

            // Alles zugeordnet: keine rote Liste, keine Warnung.
            var (fenster2, host2) = Zeige(Plan(mitSperren: false));
            Assert.Equal(Visibility.Collapsed, Rahmen(RoteListe(host2)).Visibility);
            Assert.DoesNotContain(VisualTreeSafe.FindDescendants<TextBlock>(host2).Where(Sichtbar),
                t => t.Text.Contains("gesperrt"));
            fenster2.Close();

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
