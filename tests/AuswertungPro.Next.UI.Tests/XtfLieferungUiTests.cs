using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class XtfLieferungUiTests
{
    [Fact]
    public async Task Entwurf_sperrt_Wechsel_Pruefung_und_Schliessen_bis_zum_Speichern()
    {
        var store = new Ablage(); var vm = new XtfLieferungViewModel(store, new Dialoge());
        await vm.OeffnenCommand.ExecuteAsync(null);
        vm.Auswahl = vm.Zeilen[0]; await vm.AuswahlLaden;
        vm.Felder.Single(f => f.Feld.Schluessel == "Textinhalt").Wert = "Neuer Text";
        var vorher = vm.Auswahl; vm.Auswahl = vm.Zeilen[1];
        Assert.Same(vorher, vm.Auswahl); Assert.False(vm.DarfSchliessen());
        await vm.PruefenCommand.ExecuteAsync(null); Assert.Equal(0, store.Pruefungen);
        await vm.SpeichernCommand.ExecuteAsync(null);
        Assert.Equal("Neuer Text", store.Werte["Textinhalt"]); Assert.False(vm.HatEntwurf);
        Assert.Equal("Neuer Text", vm.Auswahl!.Bezeichnung); Assert.True(vm.DarfSchliessen());
        await vm.PruefenCommand.ExecuteAsync(null);
        Assert.Equal(1, store.Pruefungen); Assert.Contains("Vollständigen Bericht", vm.BerichtVorschau);
        Assert.True(vm.BerichtVorschau.Length < 6200);
        await vm.BerichtSpeichernCommand.ExecuteAsync(null); Assert.True(store.BerichtGespeichert);
    }

    [Fact]
    public async Task Speicherfehler_erhaelt_die_Eingaben_und_Verwerfen_stellt_den_Originalwert_wieder_her()
    {
        var store = new Ablage { Speicherfehler = true }; var vm = new XtfLieferungViewModel(store, new Dialoge());
        await vm.OeffnenCommand.ExecuteAsync(null); vm.Auswahl = vm.Zeilen[0]; await vm.AuswahlLaden;
        vm.Felder[0].Wert = "Eingabe"; await vm.SpeichernCommand.ExecuteAsync(null);
        Assert.True(vm.HatEntwurf); Assert.Contains("zwischenzeitlich", vm.Status);
        vm.VerwerfenCommand.Execute(null); Assert.False(vm.HatEntwurf); Assert.Equal("Originaltext", vm.Felder[0].Wert);
    }

    [Fact]
    public async Task Echtes_Fenster_behaelt_Altwerte_und_bietet_die_vollstaendigen_Normlisten()
    {
        var r = await WpfIsolatedTestProcess.RunAsync(typeof(XtfLieferungUiTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(90));
        Assert.False(r.TimedOut, r.DescribeFailure()); Assert.True(r.ExitCode == 0 && r.ChildScenarioCompleted, r.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            foreach (var path in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + path, UriKind.Relative) });
            var store = new Ablage(); var vm = new XtfLieferungViewModel(store, new Dialoge());
            var window = new XtfLieferungWindow(vm) { Left = -20000, Top = -20000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show(); Warte(window, vm.OeffnenCommand.ExecuteAsync(null));
            vm.Auswahl = vm.Zeilen[0]; Warte(window, vm.AuswahlLaden);
            var feld = vm.Felder.Single(f => f.Feld.Schluessel == "TextVAli");
            var combo = VisualTreeSafe.FindDescendants<ComboBox>(window).Single(c => ReferenceEquals(c.DataContext, feld));
            Assert.Equal(new[] { "Top", "Cap", "Half", "Base", "Bottom" }, combo.Items.Cast<string>());
            Assert.Equal("Altwert", feld.Wert); Assert.Contains("Altwert", feld.Hinweis); Assert.False(vm.HatEntwurf);
            combo.IsDropDownOpen = true; Pump(window); combo.SelectedItem = "Bottom"; combo.IsDropDownOpen = false; Pump(window);
            Assert.Equal("Bottom", feld.Wert); Assert.True(vm.HatEntwurf);
            window.Close(); Assert.True(window.IsVisible);
            Warte(window, vm.SpeichernCommand.ExecuteAsync(null)); Assert.Equal("Bottom", store.Werte["TextVAli"]);
            Bild(window);
            window.Width = 900; window.Height = 680; Pump(window); Bild(window, "xtf-lieferung-nova-schmal");
            app.Resources.MergedDictionaries[0] = new ResourceDictionary { Source = new Uri("/SewerStudio;component/Theme/Theme.xaml", UriKind.Relative) };
            Pump(window); Bild(window, "xtf-lieferung-nova-dunkel-schmal");
            window.Width = 1240; window.Height = 860; Pump(window); Bild(window, "xtf-lieferung-nova-dunkel");
            // Der vorhandene Themenwechsel darf weder Eingaben noch die letzte Auswahl verlieren.
            Assert.Equal("Bottom", vm.Felder.Single(f => f.Feld.Schluessel == "TextVAli").Wert);
            Assert.False(vm.HatEntwurf); Assert.Equal(5, vm.Felder.Count);
            window.Close(); WpfIsolatedTestProcess.MarkChildScenarioCompleted(); app.Shutdown();
        });
    }
    private static void Warte(Window w, Task task)
    {
        var limit = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < limit) Pump(w);
        Assert.True(task.IsCompleted, "UI-Vorgang blieb hängen."); task.GetAwaiter().GetResult(); Pump(w);
    }
    private static void Pump(Window w) { w.UpdateLayout(); w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Bild(Window window, string name = "xtf-lieferung-fenster")
    {
        var host = (FrameworkElement)window.Content;
        var breite = (int)Math.Ceiling(host.ActualWidth + host.Margin.Left + host.Margin.Right);
        var hoehe = (int)Math.Ceiling(host.ActualHeight + host.Margin.Top + host.Margin.Bottom);
        var bmp = new RenderTargetBitmap(breite, hoehe, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawRectangle(window.Background, null, new Rect(0, 0, breite, hoehe));
        // Direkt rendern: VisualBrush würde nach dem Verkleinern alte, überstehende Tabellenbereiche mit einpassen.
        bmp.Render(visual); bmp.Render(host); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp));
        using var output = File.Create(TestRepoPaths.RepoFile(".tmp", name + ".png")); png.Save(output);
    }

    private sealed class Ablage : IXtfLieferungsAblage
    {
        public Dictionary<string, string> Werte { get; } = new() { ["Textinhalt"] = "Originaltext", ["TextVAli"] = "Altwert", ["TextHAli"] = "Left" };
        public bool Speicherfehler { get; init; }
        public int Pruefungen { get; private set; }
        public bool BerichtGespeichert { get; private set; }
        private int _version;
        public XtfLieferungsInfo Importiere(string q, string a, IProgress<string>? f = null, CancellationToken token = default) => Oeffne(a);
        public XtfLieferungsInfo Oeffne(string a) => new(a, "original.xtf", "sha", 2, _version, [new("Haltung_Text", 2)]);
        public XtfLieferungsSeite Suche(string a, string? k, string s, int seite = 0, bool nurProbleme = false) => new(2,
            [new(1, "Haltung_Text", "chTEST0000000040", Werte["Textinhalt"], _version > 0, ""), new(2, "Haltung_Text", "chTEST0000000041", "Zweiter Text", false, "")]);
        public XtfLieferungsObjekt Lies(string a, long id) => new(id, _version, "Haltung_Text", "chTEST0000000040",
            [new("Textinhalt", "Textinhalt", Werte["Textinhalt"], "Text", true, true, []),
             new("TextVAli", "Vertikale Ausrichtung", Werte["TextVAli"], "Enum", true, true, ["Top", "Cap", "Half", "Base", "Bottom"]),
             new("TextHAli", "Horizontale Ausrichtung", Werte["TextHAli"], "Enum", true, true, ["Left", "Center", "Right"]),
             new("TextPos.C1", "Rechtswert", "2690000.000", "Number", true, true, []), new("TextPos.C2", "Hochwert", "1190000.000", "Number", true, true, [])]);
        public void Speichere(string a, long id, int version, IReadOnlyDictionary<string, string> patch)
        { if (Speicherfehler) throw new InvalidOperationException("Objekt wurde zwischenzeitlich geändert."); foreach (var (k, v) in patch) Werte[k] = v; _version++; }
        public XtfLieferungsPruefung Pruefe(string a, IProgress<string>? f = null, CancellationToken token = default)
        { Pruefungen++; return new(2, 1, 0, "Prüfbericht\n" + new string('x', 8000)); }
        public XtfLieferungsPruefung Exportiere(string a, string z, IProgress<string>? f = null, CancellationToken token = default) => Pruefe(a, f, token);
        public void SichereBericht(string a, string z) => BerichtGespeichert = true;
    }
    private sealed class Dialoge : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => "arbeit.ssxtf";
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => "ausgabe" + defaultExt;
        public string[] OpenFiles(string title, string filter) => [];
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string m, string title = "Hinweis") { }
        public void Warn(string m, string title = "Warnung") { }
        public void Error(string m, string title = "Fehler") { }
        public bool Confirm(string m, string title = "Bestaetigung") => false;
        public bool ConfirmWarn(string m, string title = "Bestaetigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string m, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
