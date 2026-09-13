using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

[Collection("IsolatedWpf")]
public sealed class BearbeitungErledigtUiTests
{
    [Fact]
    public async Task Knopf_markiert_nur_die_Auswahl_und_zeigt_den_Stand_in_beiden_Ansichten()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(typeof(BearbeitungErledigtUiTests).FullName + ".Kindprozess", TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", TestRepoPaths.RepoFile(".tmp", "erledigt-test-settings"));
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var r in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("/SewerStudio;component/" + r, UriKind.Relative) });
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var services = new ServiceProvider(new AppSettings { DataAutoSaveMode = AutoSaveMode.Disabled },
                new DiagnosticsOptions(), loggerFactory.CreateLogger("test"), loggerFactory);
            ViewCustomizationStore.Configure(services.Settings);
            ViewCustomizationStore.GetOrCreate("DataPage").SplitterSizes["HaltungenEingabefelder"] = 270;
            ViewCustomizationStore.GetOrCreate("SchaechtePage").SplitterSizes["SchaechteEingabefelder"] = 270;
            using var shell = new ShellViewModel(services, new SystemMonitorService(enableHardwareSensorInit: false));
            var p = new Project();
            var h = new HaltungRecord();
            h.SetFieldValue(FieldKeys.HoldingName, "Test 101-102", FieldSource.Manual, true);
            h.SetFieldValue(FieldKeys.WorkflowStatus, "offen", FieldSource.Manual, true);
            var h2 = new HaltungRecord();
            h2.SetFieldValue(FieldKeys.HoldingName, "Test 201-202", FieldSource.Manual, true);
            var s = new SchachtRecord(); s.SetFieldValue("Schachtnummer", "Test 101");
            s.SetFieldValue("Status\noffen/abgeschlossen", "abgeschlossen", FieldSource.Manual, true);
            var s2 = new SchachtRecord(); s2.SetFieldValue("Schachtnummer", "Test 102");
            p.Data.Add(h); p.Data.Add(h2); p.SchaechteData.Add(s); p.SchaechteData.Add(s2);
            shell.ReplaceProject(p); shell.IsProjectReady = true;
            using var hv = new DataPageViewModel(shell, services);
            using var sv = new SchaechtePageViewModel(shell, services);
            p.Data.Remove(h2); p.SchaechteData.Remove(s2);
            hv.BearbeitungUmschaltenCommand.Execute(h2); sv.BearbeitungUmschaltenCommand.Execute(s2);
            Assert.False(h2.BearbeitungErledigt); Assert.False(s2.BearbeitungErledigt);
            p.Data.Add(h2); p.SchaechteData.Add(s2);
            hv.Selected = h; sv.Selected = s;
            var window = new Window { Width = 1366, Height = 768, Left = -20000, Top = -20000, ShowInTaskbar = false };
            window.Show();

            foreach (var theme in new[] { "ThemeLight.xaml", "Theme.xaml" })
            {
                services.Settings.HaltungenAnsicht = "liste";
                services.Settings.SchaechteAnsicht = "liste";
                app.Resources.MergedDictionaries[0] = new ResourceDictionary
                { Source = new Uri("/SewerStudio;component/Theme/" + theme, UriKind.Relative) };
                var hp = new Views.Pages.DataPage { DataContext = hv };
                PruefeSeite(hp, h, () => h.BearbeitungErledigt, "haltung-" + theme);
                var sp = new Views.Pages.SchaechtePage { DataContext = sv };
                PruefeSeite(sp, s, () => s.BearbeitungErledigt, "schacht-" + theme);
            }

            Assert.False(h2.BearbeitungErledigt); Assert.False(s2.BearbeitungErledigt);
            Assert.Equal("offen", h.GetFieldValue(FieldKeys.WorkflowStatus));
            Assert.Equal("abgeschlossen", s.GetFieldValue("Status\noffen/abgeschlossen"));
            Assert.True(p.Dirty);
            Assert.False(hv.BearbeitungUmschaltenCommand.CanExecute(null));
            Assert.False(sv.BearbeitungUmschaltenCommand.CanExecute(null));
            window.Content = null;
            shell.ReplaceProject(new Project());
            hv.BearbeitungUmschaltenCommand.Execute(h); sv.BearbeitungUmschaltenCommand.Execute(s);
            Assert.False(h.BearbeitungErledigt); Assert.False(s.BearbeitungErledigt);
            window.Close();
            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();

            void PruefeSeite(UserControl page, object record, Func<bool> istErledigt, string bildname)
            {
                window.Content = page; Pump(window);
                Assert.Equal(0, Assert.IsType<RowDefinition>(page.FindName("DrawerRow")).ActualHeight);
                Assert.IsType<MenuItem>(page.FindName("AnsichtListeMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump(window);
                if (record is HaltungRecord hr) hv.Selected = hr;
                else sv.Selected = (SchachtRecord)record;
                Pump(window);
                var button = VisualTreeSafe.FindDescendants<Button>(page).Single(b =>
                    AutomationProperties.GetName(b) == "Bearbeitung als erledigt markieren");
                Assert.Same(record, button.CommandParameter);
                Assert.True(button.IsEnabled);
                button.Command.Execute(button.CommandParameter); Pump(window);
                Assert.True(istErledigt());
                Assert.Equal("Erledigt-Markierung aufheben", AutomationProperties.GetName(button));
                Assert.Contains(Marken(page), m => m.IsVisible && ReferenceEquals(m.DataContext, record));
                Bild(page, bildname);

                Assert.IsType<MenuItem>(page.FindName("AnsichtTabelleMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Pump(window);
                var grid = Assert.IsType<DataGrid>(page.FindName("Grid"));
                Assert.True(grid.IsVisible);
                grid.SelectedItem = record; Pump(window);
                Assert.True(Assert.IsType<RowDefinition>(page.FindName("DrawerRow")).ActualHeight >= 120);
                Assert.Contains(Marken(grid), m => m.IsVisible && ReferenceEquals(m.DataContext, record));
                button.Command.Execute(button.CommandParameter); Pump(window);
                Assert.False(istErledigt());
                Assert.DoesNotContain(Marken(page), m => m.IsVisible && ReferenceEquals(m.DataContext, record));
            }
        });
    }

    private static IEnumerable<FluentIcon> Marken(DependencyObject root)
        => VisualTreeSafe.FindDescendants<FluentIcon>(root).Where(m => AutomationProperties.GetName(m) == "Bearbeitung erledigt");

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static void Bild(FrameworkElement element, string name)
    {
        var image = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(TestRepoPaths.RepoFile(".tmp", "erledigt-" + name + ".png")); png.Save(file);
    }
}
