using System.IO;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;

internal static class Program
{
    private sealed class ProbeApp : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // WPF stellt OnStartup bereits im Application-Konstruktor in die Warteschlange.
            // Deshalb im Pruefhost ausdruecklich ueberschreiben, ohne base-Aufruf.
            File.WriteAllText(Path.Combine(Root, "startup-unterdrueckt.txt"), DateTime.UtcNow.ToString("O"));
        }
    }
    static readonly string Root = @"C:\Sewer-Studio_KI_4.5\.tmp\nova-abschluss\bedienung";
    static readonly string Bin = @"C:\Sewer-Studio_KI_4.5\.tmp\nova-abschluss\artifacts-hauptbaum\bin\AuswertungPro.Next.UI\release";
    static string AppliedTheme = "Dark";
    [STAThread]
    static int Main(string[] args)
    {
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("SEWERSTUDIO_APPDATA_DIR", Path.Combine(Root, "profil"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT", Path.Combine(Root, "wissen"));
        Environment.SetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL", "0");
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var candidates = new[] {
                Path.Combine(Bin, "runtimes", "win-x64", "lib", "net10.0", name.Name + ".dll"),
                Path.Combine(Bin, "runtimes", "win", "lib", "net10.0", name.Name + ".dll"),
                Path.Combine(Bin, "runtimes", "win", "lib", "net8.0", name.Name + ".dll"),
                Path.Combine(Bin, name.Name + ".dll")
            };
            var path = candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        try { Run(args); return 0; }
        catch (Exception ex) { File.WriteAllText(Path.Combine(Root, "host-fehler.txt"), ex.ToString()); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run(string[] args)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var theme = args.FirstOrDefault() ?? "Dark";
        AppliedTheme = theme;
        var projectPath = Path.Combine(Root, "projekt", "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        if (!File.Exists(projectPath)) CreateProject(projectPath);
        var settings = new AppSettings
        {
            UiTheme = theme, KnowledgeRootPath = Path.Combine(Root, "wissen"),
            EvalSetRoot = Path.Combine(Root, "wissen", "eval_set"),
            AiStartOnProgramStart = false, EnableRestorePoints = false, ReduceMotion = true,
            LastProjectPath = projectPath, RecentProjectPaths = [projectPath],
            ProjectsRootDirectory = Path.Combine(Root, "projekt"),
            LastFullBackupUtc = DateTime.UtcNow,
            QgisHaltungenGpkgPath = Path.Combine(Root, "quellen", "leitungen.gpkg"),
            QgisSchaechteGpkgPath = Path.Combine(Root, "quellen", "schaechte.gpkg"),
            KatasterKennungenGpkgPath = Path.Combine(Root, "quellen", "kennungen.gpkg")
        };
        var app = new ProbeApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // App.xaml enthaelt die echten View-Templates. Nur die Ressourcen laden:
        // eine App-Unterklasse aus einer fremden Assembly kann deren BAML nicht initialisieren.
        var document = System.Xml.Linq.XDocument.Load(@"C:\Sewer-Studio_KI_4.5\src\AuswertungPro.Next.UI\App.xaml");
        var dictionary = document.Root!.Elements().Single().Elements().Single();
        dictionary.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        foreach (var attribute in dictionary.Attributes().Where(a => a.IsNamespaceDeclaration).ToArray())
            if (attribute.Value.StartsWith("clr-namespace:") && !attribute.Value.Contains(";assembly="))
                attribute.Value += ";assembly=SewerStudio";
        foreach (var element in dictionary.Descendants())
            if (element.Name.NamespaceName.StartsWith("clr-namespace:") && !element.Name.NamespaceName.Contains(";assembly="))
                element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=SewerStudio");
        foreach (var source in dictionary.Descendants().Attributes("Source"))
            source.Value = "/SewerStudio;component/" + source.Value;
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
        WindowStateManager.Configure(settings);
        ViewCustomizationStore.Configure(settings);
        MotionSettings.Configure(true);
        var themeFile = theme == "Dark" ? "Theme.xaml" : "ThemeLight.xaml";
        app.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"/SewerStudio;component/Theme/{themeFile}", UriKind.Relative)
        };
        var services = new AuswertungPro.Next.UI.ServiceProvider(settings, new DiagnosticsOptions(),
            NullLogger.Instance, NullLoggerFactory.Instance);
        typeof(App).GetField("_services", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, services);
        // ProbeApp unterdrueckt OnStartup: kein Echtzeitspiegel, keine QGIS-Bruecke,
        // kein KI-Prozessstart. Echte Produktdienste, Fenster und Bedienlogik bleiben aktiv.
        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual,
            Width = 1366, Height = 768, Left = 50, Top = 50 };
        app.MainWindow = window;
        window.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { if (window.IsVisible) Measure(window); };
        window.Closed += (_, _) => { timer.Stop(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); };
        timer.Start();
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.AppendAllText(Path.Combine(Root, "ui-fehler.txt"), e.Exception + Environment.NewLine);
        };
        File.WriteAllText(Path.Combine(Root, "isolation.json"), JsonSerializer.Serialize(new {
            process = Environment.ProcessId, productAssembly = typeof(App).Assembly.Location,
            profile = AppSettings.AppDataDir, services.KnowledgeRoot, settings.EvalSetRoot,
            applicationOnStartupInvoked = false, realtimeMirrorStarted = false, qgisBridgeStarted = false,
            settings.QgisHaltungenGpkgPath, settings.QgisSchaechteGpkgPath, settings.KatasterKennungenGpkgPath
        }, new JsonSerializerOptions { WriteIndented = true }));
        Dispatcher.Run();
    }

    static void CreateProject(string path)
    {
        var project = new Project { Name = "Nova Abschlussprobe", Description = "Nur kuenstliche Daten" };
        project.EnsureMetadataDefaults();
        for (var i = 0; i < 14; i++)
        {
            var record = project.CreateNewRecord();
            void Set(string key, string value) => record.SetFieldValue(key, value, FieldSource.Manual, false);
            Set(FieldKeys.HoldingName, $"{10001+i}-{10002+i}");
            Set("NR", (i+1).ToString());
            Set(FieldKeys.Street, i == 0 ? "" : "Teststrasse");
            Set(FieldKeys.PipeMaterial, i == 0 ? "" : "Beton");
            Set(FieldKeys.NominalDiameterMm, "300");
            Set(FieldKeys.HoldingLengthMeters, "30");
            Set(FieldKeys.ConditionClass, (i % 5).ToString());
            Set(FieldKeys.InspectionYear, "2026");
            Set(FieldKeys.UsageType, "Mischabwasser");
            Set(FieldKeys.Remarks, "Kuenstliche Bedienprobe");
            project.AddRecord(record);
        }
        var result = new JsonProjectRepository().Save(project, path);
        if (!result.Ok) throw new InvalidOperationException(result.ErrorMessage);
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        yield return element;
        for (var i=0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(element,i))) yield return child;
    }
    static void Measure(Window window)
    {
        var tree = Descendants(window).ToArray();
        var grid = tree.OfType<DataGrid>().FirstOrDefault(g => g.Name == "Grid");
        var viewport = tree.OfType<ScrollContentPresenter>().FirstOrDefault(v => grid is not null && grid.IsAncestorOf(v));
        var rows = tree.OfType<DataGridRow>().Where(r => r.IsVisible && grid is not null && grid.IsAncestorOf(r)).ToArray();
        var fullRows = viewport is null ? 0 : rows.Count(r => {
            var bounds = r.TransformToAncestor(viewport).TransformBounds(new Rect(r.RenderSize));
            return bounds.Top >= -0.1 && bounds.Bottom <= viewport.ActualHeight + 0.1;
        });
        var dpi = VisualTreeHelper.GetDpi(window);
        File.WriteAllText(Path.Combine(Root, "messung.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow, theme = AppliedTheme,
            width = window.ActualWidth, height = window.ActualHeight,
            left = window.Left, top = window.Top,
            primaryWidth = SystemParameters.PrimaryScreenWidth, primaryHeight = SystemParameters.PrimaryScreenHeight,
            dpiX = dpi.PixelsPerInchX, dpiY = dpi.PixelsPerInchY, fullRows,
            windowCount = System.Windows.Application.Current?.Windows.Count,
            selected = grid?.SelectedItem?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
