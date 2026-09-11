using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Controls;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class AufklappLayoutBedienTests
{
    private sealed class ProbeApp : System.Windows.Application
    {
        // ShowDialog pumpt die Dispatcher-Warteschlange: niemals produktiven Startup ausfuehren.
        protected override void OnStartup(StartupEventArgs e) { }
    }

    [Fact]
    public async Task Gestaltung_und_gespeicherte_Listenansicht_funktionieren_isoliert()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            GetType().FullName + "." + nameof(Kindprozess_prueft_Gestaltung), TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_Gestaltung()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewer-layout-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", Path.Combine(root, "profil"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", Path.Combine(root, "wissen"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL", "0");
        StaTestRunner.Run(() =>
        {
            var app = new ProbeApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var path in new[] { "Theme/Theme.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    { Source = new Uri("/SewerStudio;component/" + path, UriKind.Relative) });
            foreach (var theme in new[] { "Theme.xaml", "ThemeLight.xaml" })
            {
                app.Resources.MergedDictionaries[0] = new ResourceDictionary
                    { Source = new Uri("/SewerStudio;component/Theme/" + theme, UriKind.Relative) };
                PruefeDialog();
            }
            PruefeController();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static void PruefeDialog()
    {
        var original = Gruppen();
        var fenster = TestFenster(original);
        Exception? fehler = null;
        fenster.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try
            {
                var details = (RecordDetailsView)fenster.FindName("Details");
                var a = details.Groups![0].Items[0];
                var knopf = Nachfahren<Button>(details).Single(b =>
                    ReferenceEquals(b.DataContext, a) && (string?)b.ToolTip == "Dieses Feld ausblenden (Wert und Exporte bleiben unverändert)");
                Klicke(knopf);
                Assert.True(a.IsHiddenByUser);
                Assert.False(original[0].Items[0].IsHiddenByUser);
                Assert.Contains(a, details.HiddenFields!);
                fenster.UpdateLayout();
                Klicke(Nachfahren<Button>(details).Single(b => ReferenceEquals(b.DataContext, a)
                    && (string?)b.ToolTip == "Wieder einblenden"));
                Assert.False(a.IsHiddenByUser);
                details.Groups = RecordDetailDragOperations.MoveField(details.Groups!, "Stamm", 0, "Weitere", 0);
                Assert.Equal("A", details.Groups![1].Items[0].FieldName);
                // Die Standardtaste stellt Reihenfolge UND Ausblendung wieder her.
                Klicke(Knopf(fenster, "Standard wiederherstellen"));
                fenster.UpdateLayout();
                Assert.Equal(new[] { "A", "B" }, details.Groups![0].Items.Select(i => i.FieldName));
                var b = details.Groups[0].Items[1];
                Klicke(Nachfahren<Button>(details).Single(k => ReferenceEquals(k.DataContext, b)
                    && (string?)k.ToolTip == "Dieses Feld ausblenden (Wert und Exporte bleiben unverändert)"));
                Klicke(Knopf(fenster, "Speichern"));
            }
            catch (Exception ex) { fehler = ex; fenster.Close(); }
        }));
        var gespeichert = fenster.ShowDialog();
        Assert.True(fehler is null, fehler?.ToString());
        Assert.True(gespeichert);
        Assert.Equal(new[] { "B" }, fenster.Ergebnis!.HiddenFields);
        Assert.All(original.SelectMany(g => g.Items), i => Assert.False(i.IsHiddenByUser));

        var abbruch = TestFenster(original);
        abbruch.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            ((RecordDetailsView)abbruch.FindName("Details")).Groups![0].Items[0].IsHiddenByUser = true;
            Assert.True(Knopf(abbruch, "Abbrechen").IsCancel);
            abbruch.Close();
        }));
        Assert.False(abbruch.ShowDialog());
        Assert.Null(abbruch.Ergebnis);
        Assert.False(original[0].Items[0].IsHiddenByUser);
    }

    private static void PruefeController()
    {
        using var logger = LoggerFactory.Create(_ => { });
        var settings = new AppSettings { EnableRestorePoints = false };
        settings.DataPageLayout.DetailLayout.HiddenFields = ["A"];
        settings.SchaechtePageLayout.DetailLayout.HiddenFields = ["B"];
        var services = new ServiceProvider(settings, new DiagnosticsOptions(), logger.CreateLogger("test"), logger);
        using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
        var h1 = new HaltungRecord();
        var h2 = new HaltungRecord();
        shell.Project.Data.Add(h1);
        shell.Project.Data.Add(h2);
        shell.NavigateToHolding(h1);
        var hvm = Assert.IsType<DataPageViewModel>(shell.CurrentPage);
        var hl = new HaltungAufklappListe { ItemsSource = hvm.Records };
        using var hc = new DataPageAufklappListeController(hl, () => hvm, _ => Gruppen());
        hc.Verdrahte();
        hl.KlappeAuf(h1);
        Assert.Equal(new[] { "B", "C" }, Felder(hl.Themen));
        hl.KlappeAuf(h2);
        Assert.Equal(new[] { "B", "C" }, Felder(hl.Themen));
        var s = new SchachtRecord();
        shell.Project.SchaechteData.Add(s);
        shell.NavigateToShaft(s);
        var svm = Assert.IsType<SchaechtePageViewModel>(shell.CurrentPage);
        var sl = new SchachtAufklappListe { ItemsSource = svm.Records };
        using var sc = new SchaechteAufklappListeController(sl, () => svm, _ => Gruppen());
        sc.Verdrahte();
        sl.KlappeAuf(s);
        Assert.Equal(new[] { "A", "C" }, Felder(sl.Themen));
    }

    private static string[] Felder(IReadOnlyList<ThemaAnzeige>? themen)
        => themen!.SelectMany(t => t.EinzelGruppe).SelectMany(g => g.Items).Select(i => i.FieldName).ToArray();
    private static AufklappLayoutWindow TestFenster(IReadOnlyList<RecordDetailGroup> gruppen)
        => new(gruppen, null) { ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
    private static RecordDetailGroup[] Gruppen() =>
        [new("Stamm", "", [Item("A"), Item("B")]), new("Weitere", "", [Item("C")])];
    private static RecordDetailItem Item(string name) => new(name, "Probe", _ => { }) { FieldName = name };
    private static Button Knopf(DependencyObject root, string text)
        => Nachfahren<Button>(root).Single(b => b.IsVisible && b.Content as string == text);
    private static void Klicke(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static IEnumerable<T> Nachfahren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Nachfahren<T>(child)) yield return deeper;
        }
    }
}
