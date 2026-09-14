using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Windows;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class GeoShopAbgleichUiTests
{
    [Fact]
    public void GeoShop_Leser_ist_als_Application_Vertrag_registriert()
    {
        using var logging = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(new AppSettings { EnableRestorePoints = false },
            new DiagnosticsOptions(), logging.CreateLogger("test"), logging);
        Assert.Same(services.GeoShop, services.GetService(typeof(IGeoShopLeser)));
        Assert.IsType<GeoShopXtfLeser>(services.GeoShop);
        Assert.Same(services.GeoShopSicherung, services.GetService(typeof(IGeoShopSicherung)));
        Assert.IsType<GeoShopSicherungsdatei>(services.GeoShopSicherung);
    }

    [Fact]
    public async Task Vorschau_ist_lesbar_und_gibt_nur_geplante_Aenderungen_frei()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(GeoShopAbgleichUiTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var fenster = new GeoShopAbgleichWindow();
            var inhalt = (FrameworkElement)fenster.Content;
            var uebernehmen = VisualTreeSafe.FindDescendants<Button>(inhalt)
                .Single(b => Equals(b.Content, "Gezeigte Änderungen übernehmen"));
            Assert.False(uebernehmen.IsEnabled);
            fenster.Zeige("Keine passenden Objekte gefunden.", false);
            Assert.False(uebernehmen.IsEnabled);
            fenster.Zeige("Quelle: Beispiel.xtf\n2 Bauteile ändern; 3 leere Fachfelder ergänzen.\n\n" +
                "--- A-B ---\nBisherige Verknüpfungen:\n  Haltung: chALT000H0000001\n" +
                "Verknüpfungen aus GeoShop:\n  Haltung: chTEST00H0000001\n  Kanal: chTEST00K0000001\n" +
                "DN_mm: (leer) → 300\nRohrmaterial: (leer) → Beton\n\n" +
                "Ohne Übernahme / zur Prüfung:\nC-D: Mehrdeutige Zuordnung – ausgelassen.", true);
            Assert.True(uebernehmen.IsEnabled);
            var bericht = VisualTreeSafe.FindDescendants<TextBox>(inhalt).Single();
            Assert.True(bericht.IsReadOnly);
            Assert.Contains("chTEST00H0000001", bericht.Text);
            var projekt = new Project(); var schacht = new SchachtRecord(); projekt.SchaechteData.Add(schacht);
            schacht.SetFieldValue("Schachtnummer", "60248", FieldSource.Legacy, false);
            schacht.SetFieldValue("Funktion", "NOD", FieldSource.Legacy, false);
            schacht.SetFieldValue("Material", "GFK-Liner", FieldSource.Legacy, false);
            schacht.SetFieldValue("Baujahr", "", FieldSource.Manual, true);
            var vorher = System.Text.Json.JsonSerializer.Serialize(projekt);
            var quelle = new GeoShopBauteil("60248", KatasterKennung.FuerSchacht("60248", null, "chTEST00A0000001", "chTEST00C0000001"),
                new Dictionary<string, string> { ["Funktion"] = "Kontrollschacht", ["Material"] = "Beton", ["Baujahr"] = "1974" });
            var plan = GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(schacht, projekt)],
                new(BauteilArt.Schacht, "Beispiel.xtf", [quelle]), mitVergleich: true);
            fenster.Zeige(plan);
            fenster.Content = null;
            var host = new Border { Background = fenster.Background, Child = inhalt };
            host.Measure(new Size(1160, 640)); host.Arrange(new Rect(0, 0, 1160, 640)); host.UpdateLayout();
            var tabelle = VisualTreeSafe.FindDescendants<DataGrid>(host).Single();
            Assert.Equal(Visibility.Visible, tabelle.Visibility);
            var boxen = VisualTreeSafe.FindDescendants<CheckBox>(tabelle).Where(c => c.DataContext is GeoShopFeldWahl).ToArray();
            Assert.Equal(3, boxen.Length);
            var funktionswahl = boxen.Single(c => ((GeoShopFeldWahl)c.DataContext).Feld == "Funktion");
            Assert.False(funktionswahl.IsChecked);
            funktionswahl.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            Assert.True(((GeoShopFeldWahl)funktionswahl.DataContext).Uebernehmen);
            Assert.False(boxen.Single(c => ((GeoShopFeldWahl)c.DataContext).Feld == "Baujahr").IsEnabled);
            Assert.Equal(vorher, System.Text.Json.JsonSerializer.Serialize(projekt));
            host.UpdateLayout();
            if (Environment.GetEnvironmentVariable("SEWER_GEOSHOP_BILD") == "1")
            {
                var bild = new RenderTargetBitmap(1160, 640, 96, 96, PixelFormats.Pbgra32);
                bild.Render(host);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bild));
                using var output = File.Create(TestRepoPaths.RepoFile(".tmp", "geoshop-vorschau.png"));
                png.Save(output);
            }
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            fenster.Close(); app.Shutdown();
        });
    }
}
