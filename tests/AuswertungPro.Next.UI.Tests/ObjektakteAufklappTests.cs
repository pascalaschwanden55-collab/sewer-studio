using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class ObjektakteAufklappTests
{
    [Fact]
    public async Task Vollstaendige_Felder_stehen_in_beiden_Listen_und_behalten_Eingaben()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(typeof(ObjektakteAufklappTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", TestRepoPaths.RepoFile(".tmp", "objektakte-inline-settings"));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var r in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + r, UriKind.Relative) });
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(new AppSettings { DataAutoSaveMode = AutoSaveMode.Disabled }, new DiagnosticsOptions(), loggerFactory.CreateLogger("test"), loggerFactory);
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            var p = new Project();
            var h = new HaltungRecord(); h.SetFieldValue(FieldKeys.HoldingName, "Test 101–102", FieldSource.Manual, true);
            var h2 = new HaltungRecord(); h2.SetFieldValue(FieldKeys.HoldingName, "Test 201–202", FieldSource.Manual, true);
            p.Data.Add(h); p.Data.Add(h2);
            var s = new SchachtRecord(); s.SetFieldValue("Schachtnummer", "Test 101"); p.SchaechteData.Add(s);
            shell.ReplaceProject(p); shell.IsProjectReady = true;
            using var hv = new DataPageViewModel(shell, services);
            using var sv = new SchaechtePageViewModel(shell, services);
            var liste = new HaltungAufklappListe { ItemsSource = hv.Records };
            using var controller = new DataPageAufklappListeController(liste, () => hv, _ => []);
            controller.Verdrahte();
            var window = new Window { Content = liste, Width = 1280, Height = 840, Left = -20000, Top = -20000,
                ShowInTaskbar = false, Background = (Brush)app.FindResource("BgLightBrush") };
            window.Show(); Pump(window);
            Assert.Empty(Alle<ObjektakteView>(liste));
            liste.KlappeAuf(h); Pump(window);
            var view = Assert.Single(Alle<ObjektakteView>(liste));
            var vm = Assert.IsType<ObjektakteViewModel>(view.DataContext);
            Assert.Same(liste.Objektakte, vm);
            Assert.Contains(vm.Gruppen.SelectMany(g => g.Felder), f => f.Feld.Id == "haltung.flushinterval");
            Assert.Empty(p.Objektakten);
            Assert.Single(app.Windows.Cast<Window>());
            Bild(window, "haltung-alle-angaben");
            Assert.All(vm.Abschnitte, a => Assert.False(a.Offen));
            vm.Abschnitte.Single(a => a.Titel == "Daten I").Offen = true; Pump(window);
            Bild(window, "haltung-webgis-daten-i");
            vm.Suche = "Materialgruppe"; Pump(window);
            var material = Alle<ComboBox>(liste).Single(c => c.IsVisible && c.DataContext is ObjektFeldViewModel f && f.Feld.Id == "haltung.pipegroup");
            Assert.DoesNotContain(Alle<TextBox>(liste), t => t.IsVisible && t.DataContext is ObjektFeldViewModel f
                && f.Feld.Id == "haltung.pipegroup" && t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Text");
            var option = ((ObjektFeldViewModel)material.DataContext).Optionen.First(o => o.Label == "Beton");
            material.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, option); Pump(window);
            Assert.Equal(option.OriginalCode, p.Objektakten.Single(a => a.Id == h.Id).Werte["haltung.pipegroup"].Originalcode);
            var freitext = Assert.IsType<TextBox>(material.Template.FindName("PART_EditableTextBox", material));
            Keyboard.Focus(freitext); freitext.SetCurrentValue(TextBox.TextProperty, "Sonderwert aus Altbestand");
            ObjektakteView.UebernehmeEingabe(liste); Pump(window);
            Assert.Equal("Sonderwert aus Altbestand", p.Objektakten.Single(a => a.Id == h.Id).Werte["haltung.pipegroup"].Text);
            vm.Suche = "Spülintervall"; Pump(window);
            controller.AktualisiereFormular(); Pump(window);
            Assert.Same(vm, liste.Objektakte); Assert.Equal("Spülintervall", vm.Suche);
            var editor = Alle<TextBox>(liste).Single(t => t.DataContext is ObjektFeldViewModel f && f.Feld.Id == "haltung.flushinterval"
                && t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == "Text");
            Keyboard.Focus(editor); Assert.True(editor.IsKeyboardFocused);
            editor.SetCurrentValue(TextBox.TextProperty, "12");
            liste.KlappeAuf(h2); Pump(window);
            Assert.Equal("12", p.Objektakten.Single(a => a.Id == h.Id).Werte["haltung.flushinterval"].Text);
            Assert.Single(Alle<ObjektakteView>(liste));
            Assert.NotSame(vm, liste.Objektakte);
            var neuesVm = liste.Objektakte!;
            p.Data.Remove(h2); controller.AktualisiereFormular(); Pump(window);
            Assert.Null(liste.Aufgeklappt); Assert.Null(liste.Objektakte);
            Assert.Empty(Alle<ObjektakteView>(liste));
            neuesVm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "haltung.flushinterval").Text = "99";
            Assert.DoesNotContain(p.Objektakten, a => a.Id == h2.Id);

            var schaechte = new SchachtAufklappListe { ItemsSource = sv.Records };
            using var sc = new SchaechteAufklappListeController(schaechte, () => sv, _ => []);
            sc.Verdrahte(); window.Content = schaechte; schaechte.KlappeAuf(s); Pump(window);
            var sm = Assert.IsType<ObjektakteViewModel>(Assert.Single(Alle<ObjektakteView>(schaechte)).DataContext);
            Assert.Contains(sm.Gruppen.SelectMany(g => g.Felder), f => f.Feld.Id == "schacht.sohlenhoehe");
            sm.NeuerDeckelCommand.Execute(null); Pump(window);
            Assert.Equal("deckel", sm.Auswahl.Art);
            var objektwahl = Alle<ComboBox>(schaechte).Single(c => c.SelectedValuePath == "Akte");
            Assert.Same(sm.Auswahl, objektwahl.SelectedValue);
            Assert.StartsWith("Deckel", Assert.IsType<ObjektWahl>(objektwahl.SelectedItem).Titel);
            sm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "deckel.hoehe").Text = "450.94";
            sm.HauptdeckelCommand.Execute(null);
            Pump(window);
            Assert.Equal("Alle Felder", sm.Thema);
            Assert.NotEmpty(Alle<TextBox>(schaechte).Where(t => t.IsVisible && t.DataContext is ObjektFeldViewModel));
            Bild(window, "schacht-deckel-inline");
            var tabs = Alle<TabItem>(schaechte).ToArray();
            tabs.Single(t => Equals(t.Header, "Kurzansicht")).IsSelected = true; Pump(window);
            Assert.Empty(Alle<ObjektakteView>(schaechte));
            tabs.Single(t => Equals(t.Header, "Alle Angaben")).IsSelected = true; Pump(window);
            Assert.Equal("450.94", sm.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == "deckel.hoehe").Text);
            // Kompakt (11.09.2026): 1280 px Fenster -> 3 Spalten, 800 -> 2, erst unter 700 eine.
            Assert.Equal(3, Assert.Single(Alle<ObjektakteView>(schaechte)).Spalten);
            Assert.True(Assert.Single(Alle<ObjektakteView>(schaechte)).ActualHeight >= 360,
                "Die Objektakte in der Zeile braucht mindestens 360 px");
            window.Width = 800; Pump(window);
            Assert.Equal(2, Assert.Single(Alle<ObjektakteView>(schaechte)).Spalten);
            Bild(window, "schacht-deckel-schmal");
            window.Width = 640; Pump(window);
            Assert.Equal(1, Assert.Single(Alle<ObjektakteView>(schaechte)).Spalten);
            shell.ReplaceProject(new Project()); sc.AktualisiereFormular(); Pump(window);
            Assert.Null(schaechte.Objektakte); Assert.Empty(Alle<ObjektakteView>(schaechte));
            window.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted(); app.Shutdown();
        });
    }

    private static T[] Alle<T>(DependencyObject root) where T : DependencyObject => VisualTreeSafe.FindDescendants<T>(root).ToArray();
    private static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
    private static void Bild(Window window, string name)
    {
        var host = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)host.ActualWidth, (int)host.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(window.Background, null, new Rect(host.RenderSize));
            dc.DrawRectangle(new VisualBrush(host), null, new Rect(host.RenderSize));
        }
        bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(TestRepoPaths.RepoFile(".tmp", name + ".png")); png.Save(output);
    }
}
