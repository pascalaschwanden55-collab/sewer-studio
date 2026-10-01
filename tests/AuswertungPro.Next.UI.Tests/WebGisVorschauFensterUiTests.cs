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
/// Vergleichsliste SewerStudio ↔ WebGIS (Wunsch Pascal 28.09.2026, Entwurf freigegeben): links die Objekte, rechts jedes
/// Feld mit beiden Werten und was passiert; ein Objekt einzeln schreiben, vorher genau die Änderungen bestätigen.
/// Der Haken beim Vorschlag der Kanalfirma muss im Plan ankommen (Entscheid 23.09.2026 abends).
/// </summary>
[Trait(TestKategorie.Name, TestKategorie.Kindprozess)]
[Collection("IsolatedWpf")]
public sealed class WebGisVorschauFensterUiTests
{
    [Fact]
    public async Task Vergleichsliste_zeigt_felder_nebeneinander_und_schreibt_nur_nach_bestaetigung()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(WebGisVorschauFensterUiTests).FullName + "." + nameof(Kindprozess), TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    private static WebGisExportPlan Plan(out WebGisVorschlag vorschlag, out Guid haltungId)
    {
        var plan = new WebGisExportPlan();
        haltungId = Guid.NewGuid();
        var haltung = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "80480-80478", RecordId = haltungId, GlobalId = "G1",
        };
        haltung.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r-z", Feld = "Zustand", Alt = "102", AltText = "Z2", Neu = "104", NeuText = "Z4" });
        haltung.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Zustand", SewerStudio = "Z4", WebGis = "Z2", Art = WebGisVergleichsArt.Aendern, Nachher = "Z4", RefId = "r-z",
        });
        haltung.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Profiltyp", SewerStudio = "Kreisprofil (K)", WebGis = "Kreisprofil (K)", Art = WebGisVergleichsArt.Gleich,
        });
        haltung.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Länge", SewerStudio = "42.10", WebGis = "41.87", Art = WebGisVergleichsArt.BleibtImWebGis, Grund = "Länge geht nie ins WebGIS",
        });
        plan.Positionen.Add(haltung);

        var schacht = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80478", RecordId = Guid.NewGuid(), GlobalId = "G2",
        };
        vorschlag = new WebGisVorschlag
        {
            Feld = "Tiefe", Anzeige = "Tiefe [m]", AltText = "1.85", NeuText = "1.96",
            Aenderungen = { new WebGisFeldAenderung { RefId = "r-t", Feld = "Tiefe [m]", Alt = "1.85", Neu = "1.96" } },
        };
        schacht.Vorschlaege.Add(vorschlag);
        schacht.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Tiefe [m]", SewerStudio = "1.96", WebGis = "1.85", Art = WebGisVergleichsArt.Vorschlag, Nachher = "1.96",
            RefId = "r-t", Vorschlag = vorschlag,
        });
        plan.Positionen.Add(schacht);
        return plan;
    }

    private static Border Host(WebGisVorschauWindow fenster)
    {
        var inhalt = (FrameworkElement)fenster.Content;
        fenster.Content = null;
        var host = new Border { Background = fenster.Background, DataContext = fenster.DataContext, Child = inhalt };
        // NIE die Dispatcher-Warteschlange pumpen (sonst startet OnStartup im Testprozess, 23.09.2026).
        host.Measure(new Size(1440, 900)); host.Arrange(new Rect(0, 0, 1440, 900)); host.UpdateLayout();
        return host;
    }

    private static void Layout(Border host) { host.Measure(new Size(1440, 900)); host.Arrange(new Rect(0, 0, 1440, 900)); host.UpdateLayout(); }

    private static bool Sichtbar(DependencyObject element)
    {
        for (var e = element; e is not null; e = VisualTreeHelper.GetParent(e))
            if (e is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
    }

    private static List<string> Texte(Border host)
        => VisualTreeSafe.FindDescendants<TextBlock>(host).Where(Sichtbar).Select(t => t.Text).ToList();

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();

            var plan = Plan(out var vorschlag, out var haltungId);
            IReadOnlyList<WebGisSchreibZeile>? gefragt = null;
            var einzelnGeschrieben = 0;
            var geoeffnet = new List<(WebGisObjektart, Guid)>();
            var fenster = new WebGisVorschauWindow(new WebGisVorschauWindow.Ablaeufe(
                PruefeAlle: () => Task.FromResult<WebGisFensterStand?>(new WebGisFensterStand(plan, "geprüft")),
                PruefeEinzeln: (_, _) => Task.FromResult<WebGisFensterStand?>(new WebGisFensterStand(plan, "neu geprüft")),
                SchreibeEinzeln: (art, id, bestaetige) =>
                {
                    einzelnGeschrieben++;
                    Assert.Equal(WebGisObjektart.Haltung, art);
                    Assert.Equal(haltungId, id);
                    var ja = bestaetige(WebGisPlanAusschnitt.Von(plan, art, id));
                    return Task.FromResult<WebGisFensterStand?>(new WebGisFensterStand(plan, ja ? "geschrieben" : "abgebrochen"));
                },
                SchreibeAlle: _ => Task.FromResult<WebGisFensterStand?>(null),
                Oeffne: (art, id) => geoeffnet.Add((art, id))))
            {
                BestaetigungFuerTests = (zeilen, _) => { gefragt = zeilen; return false; },
            };
            fenster.PruefeAsync().GetAwaiter().GetResult();
            var host = Host(fenster);
            var stand = fenster.Stand;

            // Links beide Objekte, gewählt ist das erste mit Änderung; rechts jedes Feld mit beiden Werten.
            Assert.Equal("80480-80478", stand.Ausgewaehlt!.Name);
            var texte = Texte(host);
            Assert.Contains("SEWERSTUDIO", texte);
            Assert.Contains("WEBGIS JETZT", texte);
            Assert.Contains("WAS PASSIERT", texte);
            Assert.Contains("wird geändert", texte);
            Assert.Contains("Kreisprofil (K)", texte);
            Assert.Contains("bleibt im WebGIS", texte);
            Assert.Contains("1 Änderung", texte);
            Assert.Equal("Nur diese Haltung ins WebGIS schreiben …", stand.SchreibKnopf);
            Assert.True(stand.SchreibenAktiv);

            // «Nur Unterschiede zeigen» blendet gleiche Felder aus.
            stand.NurUnterschiede = true;
            Layout(host);
            Assert.DoesNotContain("Kreisprofil (K)", Texte(host));
            stand.NurUnterschiede = false;

            // Einzeln schreiben fragt vorher genau die Änderungen dieses Objekts ab; ohne «Ja» wird nichts geschrieben.
            var knopf = VisualTreeSafe.FindDescendants<Button>(host).Single(b => Equals(b.Content, stand.SchreibKnopf));
            knopf.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal(1, einzelnGeschrieben);
            var zeile = Assert.Single(gefragt!);
            Assert.Equal("Zustand", zeile.Feld);
            Assert.Equal("Z2", zeile.Vorher);
            Assert.Equal("Z4", zeile.Nachher);
            Assert.Equal("abgebrochen", stand.Status);

            // Schacht: Vorschlag der Kanalfirma mit Haken — der Haken kommt im Plan an.
            stand.Ausgewaehlt = stand.Objekte!.Cast<WebGisVergleichsObjekt>().Single(o => o.Name == "80478");
            Layout(host);
            Assert.False(stand.SchreibenAktiv);
            var haken = VisualTreeSafe.FindDescendants<CheckBox>(host).Where(Sichtbar).Single(c => Equals(c.Content, "mitschreiben"));
            haken.IsChecked = true;
            Assert.True(vorschlag.Gewaehlt);
            Assert.True(stand.SchreibenAktiv);
            Assert.Equal("Nur diesen Schacht ins WebGIS schreiben …", stand.SchreibKnopf);

            // Doppelklick auf die Haltung links öffnet sie in SewerStudio; das Fenster bleibt offen.
            var eintrag = VisualTreeSafe.FindDescendants<TextBlock>(host).Where(Sichtbar).First(t => t.Text == "80480-80478");
            // MouseDoubleClick ist ein direktes Ereignis der Liste; der angeklickte Text ist die Quelle.
            var liste = VisualTreeSafe.FindDescendants<ListBox>(host).Single(l => l.Name == "ObjektListe");
            liste.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = Control.MouseDoubleClickEvent, Source = eintrag });
            Assert.Equal((WebGisObjektart.Haltung, haltungId), Assert.Single(geoeffnet));
            Assert.Equal("80480-80478", stand.Ausgewaehlt!.Name);
            Assert.Contains("geöffnet", stand.Status);

            if (Environment.GetEnvironmentVariable("SEWER_WEBGIS_VORSCHAU_BILD") == "1")
            {
                var bild = new RenderTargetBitmap(1440, 900, 96, 96, PixelFormats.Pbgra32);
                bild.Render(host);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bild));
                using var output = File.Create(TestRepoPaths.RepoFile(".tmp", "webgis-vergleich.png"));
                png.Save(output);
            }
            fenster.Close();

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }
}
