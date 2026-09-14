using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class ObjektakteUiTests
{
    [Fact]
    public void Objectid_zeigt_auf_Wunsch_die_Bezeichnung_ohne_TID_oder_Originaldaten_zu_aendern()
    {
        var p = new Project(); var s = new SchachtRecord { Geonis = new() { Knoten = "chTEST00A0000001" } };
        p.SchaechteData.Add(s);
        s.SetFieldValue("Schachtnummer", "60106", FieldSource.Kataster, false);
        var vorher = System.Text.Json.JsonSerializer.Serialize(p);
        var vm = new ObjektakteViewModel(new(p, s.Id, "schacht"), new(), () => { }, () => true, () => { });
        var felder = vm.Gruppen.SelectMany(g => g.Felder).ToArray();
        var id = felder.Single(f => f.Feld.Id == "schacht.objectid");
        Assert.Equal("60106", id.Text);
        Assert.Contains("Schachtbezeichnung", id.Hinweis);
        Assert.Equal(vorher, System.Text.Json.JsonSerializer.Serialize(p));
        Assert.False(felder.Single(f => f.Feld.Id == "schacht.bezeichnung").Bearbeitbar);
        s.SetFieldValue("Schachtnummer", "59363", FieldSource.Manual, true);
        vm.AktualisiereFelder();
        Assert.Equal("59363", vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "schacht.objectid").Text);
        Assert.Equal("chTEST00A0000001", s.Geonis.Knoten);
    }

    [Theory]
    [InlineData("009123", false)]
    [InlineData("", true)]
    public void Eigene_Objectid_und_bewusst_leere_Handkennung_bleiben_erhalten(string objektid, bool hand)
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Schachtnummer", "60106", FieldSource.Kataster, false);
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new()
            { ["schacht.objectid"] = new() { Text = objektid, VonHand = hand } } });
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        Assert.Equal(objektid, b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.objectid")));
        Assert.False(SchachtObjektId.Anzeige(b, b.Wurzel).AusBezeichnung);
    }

    [Fact]
    public void Eingabe_der_Tiefe_aktualisiert_die_berechnete_Sohlenhoehe_im_offenen_Formular()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Tiefe", "2.890", FieldSource.Manual, true);
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new()
            { ["deckel.hoehe"] = new() { Text = "520.600" } } });
        var vm = new ObjektakteViewModel(new(p, s.Id, "schacht"), new(), () => { }, () => true, () => { });
        var felder = vm.Gruppen.SelectMany(g => g.Felder).ToArray();
        var sohle = felder.Single(f => f.Feld.Id == "schacht.sohlenhoehe");
        Assert.Equal("517.710", sohle.Text);
        felder.Single(f => f.Feld.Id == "schacht.tiefe").Text = "2.500";
        Assert.Equal("518.100", sohle.Text);
        Assert.Contains("Deckelhöhe − Tiefe", sohle.Hinweis);
    }

    [Fact]
    public void Leeres_Tiefenfeld_zeigt_Differenz_und_folgt_Sohlenkorrektur_bis_zur_Handeingabe()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new()
            { ["schacht.sohlenhoehe"] = new() { Text = "517.710" } } });
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new()
            { ["deckel.hoehe"] = new() { Text = "520.600" } } });
        var vorher = System.Text.Json.JsonSerializer.Serialize(p);
        var vm = new ObjektakteViewModel(new(p, s.Id, "schacht"), new(), () => { }, () => true, () => { });
        ObjektFeldViewModel Feld(string id) => vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == id);
        var tiefe = Feld("schacht.tiefe");
        Assert.Equal("2.890", tiefe.Text);
        Assert.Contains("Berechnet", tiefe.Hinweis);
        Assert.Equal(vorher, System.Text.Json.JsonSerializer.Serialize(p));
        Feld("schacht.sohlenhoehe").Text = "518.100";
        Assert.Equal("2.500", tiefe.Text);
        tiefe.Text = "2.450";
        Feld("schacht.sohlenhoehe").Text = "518.000";
        Assert.Equal("2.450", tiefe.Text);
        tiefe.Text = "";
        Feld("schacht.sohlenhoehe").Text = "517.710";
        Assert.Equal("", tiefe.Text); // Auch bewusstes Leeren bleibt geschützt.
    }

    [Theory]
    [InlineData("520,600", "517,710", "", false, "2.890")]
    [InlineData("520.600", "520.600", "", false, "0.000")]
    [InlineData("520.600", "521.000", "", false, "")]
    [InlineData("", "517.710", "", false, "")]
    [InlineData("520.600", "ungültig", "", false, "")]
    [InlineData("520.600", "517.710", "2.800", false, "2.800")]
    [InlineData("520.600", "517.710", "", true, "")]
    public void Tiefenanzeige_beachtet_Zahlen_und_vorhandene_Werte(string deckel, string sohle, string bestand, bool hand, string erwartet)
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Tiefe", bestand, FieldSource.Manual, hand);
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new()
            { ["schacht.sohlenhoehe"] = new() { Text = sohle } } });
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new()
            { ["deckel.hoehe"] = new() { Text = deckel } } });
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        Assert.Equal(erwartet, b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.tiefe")));
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id] });
        Assert.Equal(bestand, b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.tiefe")));
    }

    [Theory]
    [InlineData(400, 1)] [InlineData(699, 1)] [InlineData(700, 2)] [InlineData(1099, 2)]
    [InlineData(1100, 3)] [InlineData(1499, 3)] [InlineData(1500, 4)] [InlineData(1900, 4)]
    public void Feldspalten_folgen_der_Breite(double breite, int erwartet)
        => Assert.Equal(erwartet, Views.Controls.ObjektakteView.SpaltenFuerBreite(breite));

    [Theory]
    [InlineData(0, 620)] [InlineData(double.NaN, 620)] [InlineData(400, 360)] [InlineData(510, 360)]
    [InlineData(900, 750)] [InlineData(1000, 850)]
    public void Objektakte_in_der_Zeile_nimmt_die_Listenhoehe_minus_Kopf(double listenhoehe, double erwartet)
        => Assert.Equal(erwartet, Views.Pages.Haltungsansicht.FormularHoeheConverter.Berechne(listenhoehe));

    [Fact]
    public async Task GeoShop_Befehle_gibt_es_nur_mit_Anbindung_und_die_Maske_liest_danach_neu()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var ohne = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => { }, () => true, () => { });
        Assert.False(ohne.GeoShopErgaenzenCommand.CanExecute(null));
        Assert.False(ohne.GeoShopDateiCommand.CanExecute(null));

        var geaendert = 0; var gewaehlt = 0;
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => geaendert++, () => true, () => { },
            geoShopErgaenzen: () =>
            {
                h.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Kataster, false);
                return Task.FromResult(true);
            },
            geoShopDatei: () => gewaehlt++);
        Assert.True(vm.GeoShopErgaenzenCommand.CanExecute(null));
        await vm.GeoShopErgaenzenCommand.ExecuteAsync(null);
        Assert.Equal(1, geaendert);
        Assert.Contains("GeoShop", vm.Meldung);
        Assert.Equal("300", vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.dn").Text);
        vm.GeoShopDateiCommand.Execute(null);
        Assert.Equal(1, gewaehlt);

        // Nichts uebernommen: kein Umbau, keine Aenderungsmeldung.
        var nein = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => geaendert++, () => true, () => { },
            geoShopErgaenzen: () => Task.FromResult(false));
        await nein.GeoShopErgaenzenCommand.ExecuteAsync(null);
        Assert.Equal(1, geaendert);
    }

    [Fact]
    public void Feldmarkierung_liegt_programmweit_in_den_Einstellungen_und_nicht_im_Projekt()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var settings = new AppSettings();
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), settings, () => { }, () => true, () => { });
        var feld = vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.dn");
        Assert.Equal("", feld.Farbe);
        feld.FarbeCommand.Execute("Gelb");
        Assert.Equal("Gelb", feld.Farbe);
        Assert.Equal("Gelb", settings.ObjektakteFarben["haltung.dn"]);
        // Ein zweites Projekt sieht dieselbe Markierung - sie haengt am Feld, nicht am Datensatz.
        var p2 = new Project(); var h2 = new HaltungRecord(); p2.Data.Add(h2);
        var vm2 = new ObjektakteViewModel(new(p2, h2.Id, "haltung"), settings, () => { }, () => true, () => { });
        Assert.Equal("Gelb", vm2.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.dn").Farbe);
        feld.FarbeCommand.Execute("");
        Assert.Equal("", feld.Farbe);
        Assert.False(settings.ObjektakteFarben.ContainsKey("haltung.dn"));
        Assert.Throws<ArgumentException>(() => feld.Farbe = "Lila");
        Assert.Equal(5, ObjektFeldViewModel.Farben.Count);
        Assert.False(p.Dirty); Assert.Empty(p.Objektakten);
    }

    [Fact]
    public void Suche_springt_zu_einem_anderen_Deckel_ohne_Daten_zu_aendern()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var d = b.Neu("deckel"); b.Schreibe(d, FieldCatalog.Objektfelder.Feld("deckel.bemerkung"), "", "Klemmt am Rand");
        p.Dirty = false;
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { }) { Suche = "Klemmt" };
        var treffer = Assert.Single(vm.Suchtreffer);
        vm.TrefferOeffnenCommand.Execute(treffer);
        Assert.Same(d, vm.Auswahl); Assert.False(p.Dirty);
        Assert.All(vm.Gruppen, g => Assert.True(g.Offen));
    }
    [Fact]
    public void Versteckte_Felder_sind_suchbar_und_Ansicht_aendert_keine_Daten()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        h.SetFieldValue(FieldKeys.Remarks, "Verborgener Prüftext", FieldSource.Manual, true);
        var settings = new AppSettings();
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), settings, () => { }, () => true, () => { });
        var f = vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.remarks");
        f.Sichtbar = false;
        Assert.DoesNotContain(vm.Gruppen.SelectMany(g => g.Felder), f => f.Feld.Id == "haltung.remarks");
        vm.Suche = "Prüftext";
        Assert.Contains(vm.Gruppen.SelectMany(g => g.Felder), f => f.Feld.Id == "haltung.remarks");
        Assert.False(p.Dirty); Assert.Empty(p.Objektakten);
    }

    [Fact]
    public void Elternwechsel_loescht_keinen_Detailwert_und_unbekannter_Code_bleibt_sichtbar()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        b.Schreibe(b.Wurzel, FieldCatalog.Objektfelder.Feld("haltung.pipegroup"), "", "Beton");
        b.Schreibe(b.Wurzel, FieldCatalog.Objektfelder.Feld("haltung.material"), "", "Sondermaterial");
        b.Wurzel.Werte["haltung.material"].Originalcode = "Z999";
        var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
        var felder = vm.Gruppen.SelectMany(g => g.Felder).ToArray();
        var detail = felder.Single(f => f.Feld.Id == "haltung.material");
        felder.Single(f => f.Feld.Id == "haltung.pipegroup").Text = "Andere Gruppe";
        Assert.Empty(detail.Optionen); Assert.Equal("Sondermaterial", detail.Text);
        Assert.Contains("Z999", detail.Hinweis);
        Assert.Equal("Sondermaterial", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public async Task Nova_Objektakte_rendert_breit_und_schmal()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(typeof(ObjektakteUiTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", TestRepoPaths.RepoFile(".tmp", "objektakte-ui-settings"));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });
            var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
            s.SetFieldValue("Schachtnummer", "Beispiel 79969", FieldSource.Manual, true);
            var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
            var d = b.Neu("deckel"); b.Schreibe(d, FieldCatalog.Objektfelder.Feld("deckel.hoehe"), "", "450.94"); b.SetzeHauptdeckel(d);
            b.Schreibe(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.sohlenhoehe"), "", "448.34");
            var vm = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
            var window = new ObjektakteWindow(vm);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000; window.Top = -20000; window.ShowInTaskbar = false;
            WindowFx.SetEntrance(window, false);
            window.Show();
            var host = (FrameworkElement)window.Content;
            // Kompakt (11.09.2026): 4 Spalten auf Full HD, 3 im Standardfenster, 1 schmal.
            foreach (var (width, spalten) in new[] { (1800, 4), (1140, 3), (680, 1) })
            {
                window.Width = width;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(spalten, window.Spalten);
                var grids = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<System.Windows.Controls.Primitives.UniformGrid>(host).ToArray();
                Assert.NotEmpty(grids); Assert.All(grids, g => Assert.Equal(window.Spalten, g.Columns));
                Assert.All(AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<CheckBox>(host)
                    .Where(c => Equals(c.Content, "Sichtbar")), c => Assert.False(c.IsVisible));
                Zeichne(window, host, $"objektakte-{width}");
            }
            Assert.Contains("2", vm.Tiefe);

            // Die Aufklappliste wird wirklich gezeichnet: der Deckel steht als anklickbare
            // Zeile darin, und der Knopf zum Anlegen ist da.
            window.Width = 1140;
            vm.AlleAufCommand.Execute(null);
            window.UpdateLayout();
            var deckelliste = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<Expander>(host)
                .FirstOrDefault(e => Equals(e.Header, "Deckel"));
            Assert.NotNull(deckelliste);
            deckelliste!.SetCurrentValue(Expander.IsExpandedProperty, true);
            window.UpdateLayout();
            deckelliste.BringIntoView();
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var knoepfe = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<Button>(host).ToArray();
            Assert.Contains(knoepfe, btn => btn.Content is string text && text.Contains("450.94"));
            Assert.Contains(knoepfe, btn => System.Windows.Automation.AutomationProperties.GetName(btn)
                == "Neuer Eintrag in dieser Liste");
            Zeichne(window, host, "objektakte-liste");

            vm.Suche = "Sohlenhöhe";
            window.UpdateLayout();
            var eingabe = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<TextBox>(host)
                .Single(t => System.Windows.Automation.AutomationProperties.GetName(t)
                    == FieldCatalog.Objektfelder.Feld("schacht.sohlenhoehe").Label);
            System.Windows.Input.Keyboard.Focus(eingabe);
            Assert.True(eingabe.IsKeyboardFocused);
            // Feldmarkierung: der Hintergrund traegt die Theme-Farbe, die Beschriftung das Rechtsklick-Menue.
            var sohle = vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "schacht.sohlenhoehe");
            sohle.FarbeCommand.Execute("Gelb");
            window.UpdateLayout();
            var gelb = (SolidColorBrush)app.FindResource("MarkierungGelbBrush");
            Assert.Equal(gelb.Color, Assert.IsType<SolidColorBrush>(eingabe.Background).Color);
            Assert.NotNull(eingabe.ContextMenu);
            var beschriftung = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.FindDescendants<TextBlock>(host)
                .Single(t => t.Text == sohle.Label && t.ContextMenu is not null);
            Assert.Same(eingabe.ContextMenu, beschriftung.ContextMenu);
            sohle.FarbeCommand.Execute("");
            window.UpdateLayout();
            Assert.NotEqual(gelb.Color, (eingabe.Background as SolidColorBrush)?.Color);
            eingabe.SetCurrentValue(TextBox.TextProperty, "448.35");
            window.Close();
            Assert.Equal("448.35", b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.sohlenhoehe")));
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    /// <summary>Bildschirmfoto des Fensters. Mica malt die Flaeche selbst, deshalb wird der
    /// Fensterhintergrund vorher ausgemalt - sonst bleibt sie im Bild durchsichtig.</summary>
    private static void Zeichne(Window window, FrameworkElement host, string name)
    {
        var bitmap = new RenderTargetBitmap((int)host.ActualWidth, (int)host.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var hintergrund = new DrawingVisual();
        using (var context = hintergrund.RenderOpen())
        {
            context.DrawRectangle(window.Background, null, new Rect(0, 0, host.ActualWidth, host.ActualHeight));
            context.DrawRectangle(new VisualBrush(host), null, new Rect(0, 0, host.ActualWidth, host.ActualHeight));
        }
        bitmap.Render(hintergrund);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(TestRepoPaths.RepoFile(".tmp"));
        using var output = File.Create(TestRepoPaths.RepoFile(".tmp", name + ".png"));
        png.Save(output);
    }

    [Fact]
    public void Gruppenwechsel_zeigt_das_nachgezogene_Materialdetail_sofort_in_der_Maske()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var vm = new ObjektakteViewModel(new(p, h.Id, "haltung"), new(), () => { }, () => true, () => { });
        var gruppe = vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.pipegroup");
        var material = vm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.material");

        gruppe.Auswahl = gruppe.Optionen.Single(e => e.Label == "Beton");
        material.Auswahl = material.Optionen.Single(e => e.Label == "Beton, armiert (BA)");
        Assert.Equal("Beton, armiert (BA)", material.Text);

        gruppe.Auswahl = gruppe.Optionen.Single(e => e.Label == "Andere");

        Assert.Equal("Verschiedene (V)", material.Text);
        Assert.Equal("Verschiedene (V)", material.Auswahl?.Label);
        Assert.Contains(material.Optionen, e => e.Label == "Zement (Z)");
    }
}
