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
            foreach (var width in new[] { 1140, 680 })
            {
                window.Width = width;
                window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(width < 920 ? 1 : 2, window.Spalten);
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
}
