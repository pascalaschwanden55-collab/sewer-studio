using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Entscheid Pascal 23.09.2026 abends: Weicht ein Wert der Kanalfirma vom WebGIS ab, steht er im
/// Pruef-/Schreibfenster als eigene Zeile zum Anhaken — nie automatisch. Das Haekchen im Fenster
/// muss im Plan ankommen, sonst wuerde «Jetzt schreiben» nie etwas davon senden.
/// </summary>
[Collection("IsolatedWpf")]
public sealed class WebGisVorschauFensterUiTests
{
    [Fact]
    public async Task Abweichungen_der_kanalfirma_stehen_zum_anhaken_bereit()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(WebGisVorschauFensterUiTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    private static WebGisExportPlan PlanMitZweiVorschlaegen()
    {
        var plan = new WebGisExportPlan();
        var pos = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145", RecordId = Guid.NewGuid(),
            GlobalId = "31946755-E44D-4714-8DA6-91F56A6AC8FA",
        };
        pos.Vorschlaege.Add(new WebGisVorschlag
        {
            Feld = "Schachtform", Anzeige = "Form", AltText = "Rund", NeuText = "Oval",
            Aenderungen = { new WebGisFeldAenderung { RefId = "r-form", Feld = "Form", Alt = "1", Neu = "102" } },
        });
        pos.Vorschlaege.Add(new WebGisVorschlag
        {
            Feld = "Material", Anzeige = "Material", AltText = null, NeuText = "Beton",
            Aenderungen = { new WebGisFeldAenderung { RefId = "r-mat", Feld = "Material", Alt = null, Neu = "7" } },
        });
        plan.Positionen.Add(pos);
        return plan;
    }

    private static (WebGisVorschauWindow Fenster, Border Host) Zeige(WebGisUebersicht u)
    {
        var fenster = new WebGisVorschauWindow(
            () => Task.FromResult<WebGisUebersicht?>(u), () => Task.FromResult<WebGisUebersicht?>(null));
        fenster.PruefeAsync().GetAwaiter().GetResult(); // die Uebersicht liegt schon vor: laeuft synchron durch
        var inhalt = (FrameworkElement)fenster.Content;
        fenster.Content = null;
        var host = new Border { Background = fenster.Background, DataContext = fenster.DataContext, Child = inhalt };
        // NIE die Dispatcher-Warteschlange pumpen (sonst startet OnStartup im Testprozess, 23.09.2026).
        host.Measure(new Size(1100, 760)); host.Arrange(new Rect(0, 0, 1100, 760)); host.UpdateLayout();
        return (fenster, host);
    }

    private static bool Sichtbar(DependencyObject element)
    {
        for (var e = element; e is not null; e = VisualTreeHelper.GetParent(e))
            if (e is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private static Button Knopf(Border host, string text) => VisualTreeSafe.FindDescendants<Button>(host)
        .Single(b => Equals(b.Content, text));

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var plan = PlanMitZweiVorschlaegen();
            var (fenster, host) = Zeige(WebGisUebersicht.Aus(plan));
            var vorschlaege = plan.Positionen[0].Vorschlaege;

            // Sichtbar, mit Wert bisher und Wert der Kanalfirma, nichts vorangehakt.
            var haken = VisualTreeSafe.FindDescendants<CheckBox>(host).Where(Sichtbar).ToList();
            Assert.Equal(2, haken.Count);
            Assert.All(haken, h => Assert.False(h.IsChecked));
            var texte = VisualTreeSafe.FindDescendants<TextBlock>(host).Where(Sichtbar).Select(t => t.Text).ToList();
            Assert.Contains(texte, t => t.Contains("Kanalfirma weicht vom WebGIS ab"));
            Assert.Contains("Rund", texte);
            Assert.Contains("Oval", texte);
            Assert.Contains("(leer)", texte);
            Assert.DoesNotContain(texte, t => t == "Keine Unterschiede zum WebGIS — es gibt nichts zu übertragen.");

            // Ein Haekchen im Fenster kommt im Plan an.
            haken[0].IsChecked = true;
            Assert.True(vorschlaege[0].Gewaehlt);
            Assert.False(vorschlaege[1].Gewaehlt);

            Knopf(host, "Alle anhaken").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.All(vorschlaege, v => Assert.True(v.Gewaehlt));
            Assert.All(haken, h => Assert.True(h.IsChecked));

            Knopf(host, "Keine").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.All(vorschlaege, v => Assert.False(v.Gewaehlt));

            if (Environment.GetEnvironmentVariable("SEWER_WEBGIS_VORSCHAU_BILD") == "1")
            {
                var bild = new RenderTargetBitmap(1100, 760, 96, 96, PixelFormats.Pbgra32);
                bild.Render(host);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bild));
                using var output = File.Create(TestRepoPaths.RepoFile(".tmp", "webgis-vorschau-kanalfirma.png"));
                png.Save(output);
            }
            fenster.Close();

            // Ohne Abweichungen: kein Bereich, keine Haekchen.
            var (fenster2, host2) = Zeige(WebGisUebersicht.Aus(new WebGisExportPlan()));
            Assert.Empty(VisualTreeSafe.FindDescendants<CheckBox>(host2).Where(Sichtbar));
            fenster2.Close();

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
