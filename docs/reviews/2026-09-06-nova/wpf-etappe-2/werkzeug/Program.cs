using System.IO;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Isolierter Pruefhost der Nova-Etappe 2. Er startet NICHT das produktive Programm:
/// eigenes AppData-Verzeichnis, eigener Wissensordner, kein Application.OnStartup
/// (also kein Echtzeitspiegel, keine QGIS-Bruecke, kein KI-Prozessstart).
/// Aufruf: Pruefhost.exe &lt;Theme&gt; &lt;Seite&gt; &lt;Ausgabe.png&gt; [Breite Hoehe]
/// Seiten: Uebersicht | Haltungen | Schaechte | Import | Einstellungen | Player | TrainingStudio
/// </summary>
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

    static readonly string Root = @"C:\Sewer-Studio_KI_4.5-nova\.tmp\nova-etappe2\bedienung";
    static readonly string Bin = @"C:\Sewer-Studio_KI_4.5-nova\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows10.0.19041";
    static readonly string AppXaml = @"C:\Sewer-Studio_KI_4.5-nova\src\AuswertungPro.Next.UI\App.xaml";
    static string AppliedTheme = "Dark";
    static string AppliedPage = "Haltungen";

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
        var theme = args.ElementAtOrDefault(0) ?? "Dark";
        var seite = args.ElementAtOrDefault(1) ?? "Haltungen";
        var ausgabe = args.ElementAtOrDefault(2);
        var breite = double.TryParse(args.ElementAtOrDefault(3), out var b) ? b : 1920;
        var hoehe = double.TryParse(args.ElementAtOrDefault(4), out var h) ? h : 1080;
        AppliedTheme = theme;
        AppliedPage = seite;

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
        var document = XDocument.Load(AppXaml);
        var dictionary = document.Root!.Elements().Single().Elements().Single();
        dictionary.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
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

        // App.xaml fuehrt drei gemergte Woerterbuecher: [0] Theme, [1] Theme/Controls.xaml,
        // [2] Controls/NovaPageHeader.xaml. Nur [0] wird gegen das gewaehlte Theme getauscht.
        var themeFile = theme == "Dark" ? "Theme.xaml" : "ThemeLight.xaml";
        app.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"/SewerStudio;component/Theme/{themeFile}", UriKind.Relative)
        };

        var services = new AuswertungPro.Next.UI.ServiceProvider(settings, new DiagnosticsOptions(),
            NullLogger.Instance, NullLoggerFactory.Instance);
        typeof(App).GetField("_services", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, services);

        var window = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual,
            Width = breite, Height = hoehe, Left = 0, Top = 0 };
        app.MainWindow = window;
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.AppendAllText(Path.Combine(Root, "ui-fehler.txt"), e.Exception + Environment.NewLine);
            e.Handled = true;
        };
        window.Show();

        File.WriteAllText(Path.Combine(Root, "isolation.json"), JsonSerializer.Serialize(new {
            process = Environment.ProcessId, productAssembly = typeof(App).Assembly.Location,
            profile = AppSettings.AppDataDir, services.KnowledgeRoot, settings.EvalSetRoot,
            theme, seite, breite, hoehe,
            applicationOnStartupInvoked = false, realtimeMirrorStarted = false, qgisBridgeStarted = false,
            settings.QgisHaltungenGpkgPath, settings.QgisSchaechteGpkgPath, settings.KatasterKennungenGpkgPath
        }, new JsonSerializerOptions { WriteIndented = true }));

        var schritt = 0;
        Window? zielFenster = window;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timer.Tick += (_, _) =>
        {
            try
            {
                schritt++;
                var shell = (ShellViewModel)window.DataContext;
                if (schritt == 1)
                {
                    shell.TryOpenProject(projectPath);
                    shell.EnterWorkspaceOn(seite is "Player" or "TrainingStudio" ? "Haltungen" : seite);
                }
                else if (schritt == 2)
                {
                    // Erste Zeile waehlen: erst dann zeigen Uebersichtskarte, Rohrring und
                    // Eingabefelder echte Werte statt des Leerzustands.
                    if (shell.CurrentPage is AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel datenseite)
                    {
                        var haltung = shell.Project.Data.FirstOrDefault(
                            r => r.GetFieldValue(FieldKeys.HoldingName) == "10001-10002");
                        datenseite.Selected = haltung;
                        if (seite == "Player")
                            datenseite.PlayVideoCommand.Execute(haltung);
                    }
                    else if (shell.CurrentPage is AuswertungPro.Next.UI.ViewModels.Pages.SchaechtePageViewModel schachtseite)
                    {
                        schachtseite.Selected = shell.Project.SchaechteData.FirstOrDefault();
                    }
                }
                else if (schritt == 3)
                {
                    if (seite == "TrainingStudio")
                    {
                        var studio = new AuswertungPro.Next.UI.Views.Windows.TrainingStudioWindow(services)
                        { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
                        studio.Show();
                    }
                }
                else if (schritt == 4)
                {
                    if (seite is "Player" or "TrainingStudio")
                    {
                        // Gezielt nach dem Fenstertyp suchen: der Player oeffnet nebenbei
                        // Hilfsfenster (ForegroundWindow), die sonst zuletzt in der Liste stehen.
                        var typ = seite == "Player" ? "PlayerWindow" : "TrainingStudioWindow";
                        zielFenster = System.Windows.Application.Current!.Windows
                            .OfType<Window>().LastOrDefault(w => w.GetType().Name == typ && w.IsVisible)
                            ?? System.Windows.Application.Current!.Windows
                                .OfType<Window>().LastOrDefault(w => w != window && w.IsVisible)
                            ?? window;
                        zielFenster.WindowState = WindowState.Normal;
                        zielFenster.Width = breite;
                        zielFenster.Height = hoehe;
                        zielFenster.Left = 0;
                        zielFenster.Top = 0;
                        zielFenster.Activate();
                    }
                }
                else if (schritt >= 5)
                {
                    timer.Stop();
                    try
                    {
                        Messe(window, zielFenster!);
                        if (!string.IsNullOrWhiteSpace(ausgabe))
                            Foto(zielFenster!, ausgabe!);
                    }
                    finally
                    {
                        // Auch nach einem Fehler beenden: ein haengender Pruefhost blockiert den Lauf.
                        System.Windows.Application.Current!.Shutdown();
                        Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(Root, "ui-fehler.txt"), ex + Environment.NewLine);
            }
        };
        timer.Start();
        Dispatcher.Run();
    }

    /// <summary>Bildschirmfoto des Fensters, 1:1 in Geraetepixeln (96 dpi = keine Skalierung).</summary>
    static void Foto(Window w, string pfad)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pfad))!);
        w.UpdateLayout();
        var bmp = new RenderTargetBitmap(
            (int)Math.Round(w.ActualWidth), (int)Math.Round(w.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        bmp.Render(w);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(pfad);
        enc.Save(fs);
    }

    static void CreateProject(string path)
    {
        var project = new Project { Name = "Nova Etappe 2", Description = "Nur kuenstliche Daten" };
        project.EnsureMetadataDefaults();
        var video = Path.Combine(Root, "medien", "10001-10002.mp4");
        for (var i = 0; i < 14; i++)
        {
            var record = project.CreateNewRecord();
            void Set(string key, string value) => record.SetFieldValue(key, value, FieldSource.Manual, false);
            Set(FieldKeys.HoldingName, $"{10001 + i}-{10002 + i}");
            Set("NR", (i + 1).ToString());
            Set(FieldKeys.Street, i == 0 ? "" : "Teststrasse");
            Set(FieldKeys.PipeMaterial, i == 0 ? "" : "Beton");
            Set(FieldKeys.NominalDiameterMm, "300");
            Set(FieldKeys.HoldingLengthMeters, "30");
            Set(FieldKeys.ConditionClass, (i % 5).ToString());
            Set(FieldKeys.InspectionYear, "2026");
            Set(FieldKeys.UsageType, "Mischabwasser");
            Set(FieldKeys.Remarks, "Kuenstliche Bedienprobe");
            if (i == 0)
            {
                Set(FieldKeys.Link, video);
                record.Protocol = BaueProtokoll($"{10001 + i}-{10002 + i}");
            }
            project.AddRecord(record);
        }
        BaueSchaechte(project);
        var result = new JsonProjectRepository().Save(project, path);
        if (!result.Ok) throw new InvalidOperationException(result.ErrorMessage);
    }

    /// <summary>
    /// Zwei Beobachtungen mit echter Uhrlage und Stufe; die erste traegt einen offenen
    /// KI-Befund (Ai.Accepted = false). Damit zeigen Rohrring, Uebersicht und der
    /// Aufgaben-Chip "Naechste Aufgabe" echte Werte statt Rueckfallwerte.
    /// </summary>
    static ProtocolDocument BaueProtokoll(string haltung)
    {
        ProtocolEntry Eintrag(string code, string text, double meter, string von, string bis, string stufe, bool kiOffen)
        {
            var e = new ProtocolEntry
            {
                Code = code,
                Beschreibung = text,
                MeterStart = meter,
                MeterEnd = meter,
                Source = kiOffen ? ProtocolEntrySource.Ai : ProtocolEntrySource.Manual,
                CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe }
            };
            e.CodeMeta.Parameters["Uhr_von"] = von;
            e.CodeMeta.Parameters["Uhr_bis"] = bis;
            if (kiOffen)
                e.Ai = new ProtocolEntryAiMeta
                {
                    SuggestedCode = code,
                    Confidence = 0.72,
                    Accepted = false,
                    Reason = "Kuenstlicher Vorschlag der Bedienprobe"
                };
            return e;
        }

        var current = new ProtocolRevision();
        current.Entries.Add(Eintrag("BAB", "Riss laengs", 4.2, "10", "2", "4", kiOffen: true));
        current.Entries.Add(Eintrag("BBC", "Ablagerung", 12.8, "5", "7", "2", kiOffen: false));
        var original = new ProtocolRevision();
        foreach (var e in current.Entries)
            original.Entries.Add(ProtocolEntryCloner.CloneLegacyProtocolEntry(e));
        return new ProtocolDocument { HaltungId = haltung, Original = original, Current = current };
    }

    /// <summary>Sechs Schaechte; der erste ist oval mit zwei verschiedenen Innenmassen.</summary>
    static void BaueSchaechte(Project project)
    {
        for (var i = 0; i < 6; i++)
        {
            var schacht = new SchachtRecord();
            void Set(string key, string value) => schacht.SetFieldValue(key, value, FieldSource.Manual, false);
            Set("Schachtnummer", (10001 + i).ToString());
            Set("Strasse", "Teststrasse");
            Set("Funktion", "Normschacht");
            Set("Material", "Beton");
            Set(FieldKeys.ConditionClass, (i % 5).ToString());
            if (i == 0)
            {
                Set(FieldKeys.ShaftShape, "Oval");
                Set(FieldKeys.ShaftDimension1Mm, "1100");
                Set(FieldKeys.ShaftDimension2Mm, "900");
            }
            else
            {
                Set(FieldKeys.ShaftShape, "Rund");
                Set(FieldKeys.ShaftDimension1Mm, "600");
                Set(FieldKeys.ShaftDimension2Mm, "600");
            }
            project.SchaechteData.Add(schacht);
        }
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        yield return element;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(element, i)))
                yield return child;
    }

    static void Messe(Window window, Window ziel)
    {
        var tree = Descendants(window).ToArray();
        var grid = tree.OfType<DataGrid>().FirstOrDefault(g => g.Name == "Grid");
        var viewport = tree.OfType<ScrollContentPresenter>().FirstOrDefault(v => grid is not null && grid.IsAncestorOf(v));
        var rows = tree.OfType<DataGridRow>().Where(r => r.IsVisible && grid is not null && grid.IsAncestorOf(r)).ToArray();
        var fullRows = viewport is null ? 0 : rows.Count(r =>
        {
            var bounds = r.TransformToAncestor(viewport).TransformBounds(new Rect(r.RenderSize));
            return bounds.Top >= -0.1 && bounds.Bottom <= viewport.ActualHeight + 0.1;
        });
        var dpi = VisualTreeHelper.GetDpi(window);
        File.WriteAllText(Path.Combine(Root, $"messung-{AppliedPage}-{AppliedTheme}.json"), JsonSerializer.Serialize(new
        {
            utc = DateTime.UtcNow,
            theme = AppliedTheme,
            seite = AppliedPage,
            width = window.ActualWidth,
            height = window.ActualHeight,
            zielFenster = ziel.GetType().Name,
            zielBreite = ziel.ActualWidth,
            zielHoehe = ziel.ActualHeight,
            primaryWidth = SystemParameters.PrimaryScreenWidth,
            primaryHeight = SystemParameters.PrimaryScreenHeight,
            dpiX = dpi.PixelsPerInchX,
            dpiY = dpi.PixelsPerInchY,
            fullRows,
            windowCount = System.Windows.Application.Current?.Windows.Count,
            selected = grid?.SelectedItem?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
