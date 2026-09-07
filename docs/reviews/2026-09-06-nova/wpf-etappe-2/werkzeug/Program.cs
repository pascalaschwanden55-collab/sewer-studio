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
/// Aufruf: Pruefhost.exe &lt;Theme&gt; &lt;Seite&gt; &lt;Ausgabe.png&gt; [Breite Hoehe] [Variante]
/// Seiten: Uebersicht | Haltungen | Schaechte | Import | Einstellungen | Player | TrainingStudio
/// Varianten (Etappe 2b): "" = erste Zeile gewaehlt (Standard) | "alle" = zusaetzlich die
/// Spaltenansicht "Alle Spalten" waehlen | "ohneauswahl" = keine Zeile waehlen (Leerzustand).
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
    static string AppliedVariant = "";

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
        var variante = (args.ElementAtOrDefault(5) ?? string.Empty).Trim().ToLowerInvariant();
        AppliedTheme = theme;
        AppliedPage = seite;
        AppliedVariant = variante;

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

        // Fixwelle Runde 2: Der Pruefhost startet bewusst MIT einem alten gespeicherten
        // Spaltenlayout — so, wie es in einer bestehenden Installation liegt: alles
        // linksbuendig, alles auf Kopfbreite. Ohne das wuerde jedes Bild nur den Neuaufbau
        // zeigen und die einmalige Migration (ZahlenRechtsMigration) waere nicht belegt.
        // Nur die Haltungsseite: Ein gespeichertes Layout legt auch die Spaltenreihenfolge
        // fest. Fuer die Schachtseite ist die echte Reihenfolge des Projekts hier nicht
        // bekannt, und eine erfundene wuerde das Bild verfaelschen — die Migration ist dort
        // ueber ZahlenRechtsMigrationTests belegt.
        settings.DataPageLayout = AltesLayout();

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
                    // Variante "ohneauswahl": bewusst nichts waehlen, damit die Uebersicht
                    // ihren Leerzustand zeigt (Etappe 2b, Task 6).
                    if (variante == "ohneauswahl")
                    {
                        // nichts tun
                    }
                    // Erste Zeile waehlen: erst dann zeigen Uebersichtskarte, Rohrring und
                    // Eingabefelder echte Werte statt des Leerzustands.
                    else if (shell.CurrentPage is AuswertungPro.Next.UI.ViewModels.Pages.DataPageViewModel datenseite)
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
                    // Variante "alle": denselben Weg wie ein Klick des Benutzers gehen — den
                    // Chip der Spaltenansicht ausloesen. Damit gilt genau die Programmlogik.
                    if (variante == "alle")
                        WaehleSpaltenansicht(window, "alle");
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
                        // Fluent.Backdrop="Mica" setzt Window.Background auf Transparent; die
                        // Flaeche malt danach der Windows-Compositor, den RenderTargetBitmap
                        // NICHT erfasst. Ohne diese Zeile ist die Kopfleiste im Bild durchsichtig
                        // und jede Kontrastbeurteilung daran waere falsch.
                        MaleMicaFlaecheAus(window);
                        MaleMicaFlaecheAus(zielFenster!);
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

    /// <summary>
    /// Loest den Spaltenansicht-Chip mit dem gesuchten Schluessel aus (Tag == Schluessel).
    /// Bewusst ueber das Click-Ereignis des Umschalters: so laeuft genau der Weg des
    /// Benutzers samt Speichern der Ansicht, ohne eine zweite Steuerlogik im Pruefhost.
    /// </summary>
    static void WaehleSpaltenansicht(Window window, string schluessel)
    {
        var chip = Descendants(window)
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .FirstOrDefault(t => (t.Tag as string) == schluessel);
        if (chip is null)
        {
            File.AppendAllText(Path.Combine(Root, "ui-fehler.txt"),
                $"Spaltenansicht-Chip '{schluessel}' nicht gefunden{Environment.NewLine}");
            return;
        }
        chip.IsChecked = true;
        chip.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
    }

    /// <summary>
    /// Ersetzt den durchsichtigen Mica-Hintergrund durch die Theme-Flaeche. Nur fuer das
    /// Bildschirmfoto: Im laufenden Programm zeichnet Windows die Mica-Flaeche selbst.
    /// </summary>
    static void MaleMicaFlaecheAus(Window w)
    {
        if (w.Background is SolidColorBrush { Color.A: < 255 } or null)
            w.Background = (Brush?)w.TryFindResource("BgBrush")
                ?? (Brush?)w.TryFindResource("BgLightBrush")
                ?? System.Windows.Media.Brushes.White;
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

    static readonly string[] Strassen = ["Gotthardstrasse", "Dorfstrasse", "Bahnhofweg", "Seestrasse", "Kirchgasse"];
    static readonly string[] Materialien = ["Beton", "Steinzeug", "PVC", "PE", "Guss"];
    static readonly string[] Nennweiten = ["200", "250", "300", "400", "500", "600", "800"];
    static readonly string[] Laengen = ["24.6", "31.2", "38.5", "42.0", "17.8", "55.4", "63.1", "29.9"];

    /// <summary>
    /// Etappe 2b: 40 Haltungen, die dem echten Projekt aehneln — gemischte Pruefstaende,
    /// Zustandsklassen 0 bis 4 UND leer, mehrzeilige "Primaere Schaeden" wie aus einem
    /// Import, bei jeder zweiten Haltung ein Videopfad und bei jeder dritten eine PDF.
    /// Alles kuenstlich; es wird keine Kundendatei gelesen.
    /// </summary>
    static void CreateProject(string path)
    {
        var project = new Project { Name = "Nova Etappe 2b", Description = "Nur kuenstliche Daten" };
        project.EnsureMetadataDefaults();
        var video = Path.Combine(Root, "medien", "10001-10002.mp4");
        var pdf = Path.Combine(Root, "medien", "protokoll.pdf");
        for (var i = 0; i < 40; i++)
        {
            var name = $"{10001 + i}-{10002 + i}";
            var record = project.CreateNewRecord();
            void Set(string key, string value) => record.SetFieldValue(key, value, FieldSource.Manual, false);
            Set(FieldKeys.HoldingName, name);
            Set("NR", (i + 1).ToString());
            Set(FieldKeys.Street, Strassen[i % Strassen.Length]);
            // Der Haltungsname haengt an den beiden Schaechten; ohne diese zwei Felder
            // haette die Gruppe "Stammdaten" im echten Projekt zwei Eingaben weniger.
            Set("Schacht_oben", (10001 + i).ToString());
            Set("Schacht_unten", (10002 + i).ToString());
            Set("Inspektionsrichtung", i % 5 == 4 ? "gegen Fliessrichtung" : "in Fliessrichtung");
            Set(FieldKeys.PipeMaterial, Materialien[i % Materialien.Length]);
            Set(FieldKeys.NominalDiameterMm, Nennweiten[i % Nennweiten.Length]);
            Set(FieldKeys.ProfileType, i % 7 == 3 ? "Eiprofil" : "Kreisprofil");
            Set(FieldKeys.ClearWidthMm, Nennweiten[i % Nennweiten.Length]);
            Set(FieldKeys.HoldingLengthMeters, Laengen[i % Laengen.Length]);
            // Sechster Fall bleibt bewusst leer: der Chip muss dann den gestrichelten
            // Strich "nicht berechnet" zeigen und nie eine erfundene Klasse.
            if (i % 6 != 5)
                Set(FieldKeys.ConditionClass, (i % 5).ToString());
            Set(FieldKeys.InspectionYear, (2024 + i % 3).ToString());
            Set(FieldKeys.ConstructionYear, (1960 + i % 40).ToString());
            Set(FieldKeys.UsageType, i % 3 == 0 ? "Mischabwasser" : i % 3 == 1 ? "Schmutzabwasser" : "Regenabwasser");
            Set(FieldKeys.SlopePromille, (5 + i % 12).ToString());
            Set(FieldKeys.Owner, i % 2 == 0 ? "Gemeinde" : "Privat");
            Set(FieldKeys.Remarks, "Kuenstliche Bedienprobe");
            Set(FieldKeys.PrimaryDamages, PrimaereSchaeden(i));

            // Fuenf Pruefstaende im Wechsel, damit alle Ampel- und Pruefungstexte im Bild
            // stehen. Fall 3 kam mit der Fixwelle (F2) dazu: KI gerechnet, alles bestaetigt,
            // Haltung aber noch nicht abgeschlossen — die Ampel sagt dann "bestätigt".
            switch (i % 5)
            {
                case 0: // fachlich geprueft, KI-Befunde bestaetigt
                    Set(FieldKeys.WorkflowStatus, "abgeschlossen");
                    record.Protocol = BaueProtokoll(name, offeneKiBefunde: 0);
                    break;
                case 1: // KI analysiert, Pruefung offen (zwei offene Befunde)
                    record.Protocol = BaueProtokoll(name, offeneKiBefunde: 2);
                    break;
                case 2: // fachlich geprueft, aber noch ein offener KI-Befund
                    Set(FieldKeys.WorkflowStatus, "abgeschlossen");
                    record.Protocol = BaueProtokoll(name, offeneKiBefunde: 1);
                    break;
                case 3: // KI gerechnet, alles bestaetigt, noch nicht abgeschlossen
                    Set(FieldKeys.WorkflowStatus, "offen");
                    record.Protocol = BaueProtokoll(name, offeneKiBefunde: 0);
                    break;
                default: // nicht analysiert
                    Set(FieldKeys.WorkflowStatus, "offen");
                    break;
            }

            if (i % 2 == 0) Set(FieldKeys.Link, video);
            if (i % 3 == 0) Set(FieldKeys.PdfPath, pdf);
            if (i % 5 == 0)
            {
                Set(FieldKeys.RenovationDecision, "Ja");
                Set(FieldKeys.RecommendedRehabilitationMeasures, "Kurzliner, Anschluss verpressen");
                Set(FieldKeys.Cost, (1800 + i * 125).ToString());
            }
            project.AddRecord(record);
        }
        BaueSchaechte(project);
        var result = new JsonProjectRepository().Save(project, path);
        if (!result.Ok) throw new InvalidOperationException(result.ErrorMessage);
    }

    /// <summary>
    /// Mehrzeiliger Schadenstext wie aus einem echten Import (Meter, Code, Uhrlage, Stufe).
    /// Vier Zeilen: damit ist im Bild belegbar, dass die Zelle in "Alle Spalten" auf
    /// hoechstens drei Zeilen begrenzt wird und den Rest nur im Tooltip fuehrt.
    /// </summary>
    static string PrimaereSchaeden(int i)
    {
        if (i % 4 == 3) return string.Empty;
        var zeilen = new List<string>
        {
            $"{(4.2 + i % 7):0.00} m  BABA  Riss laengs, 10-2 Uhr, Stufe 4",
            $"{(12.8 + i % 5):0.00} m  BBCA  Ablagerung Sand, Querschnitt 15 %, 5-7 Uhr, Stufe 2",
            $"{(21.3 + i % 9):0.00} m  BAJB  Verschobene Rohrverbindung versetzt, 3 Uhr, Stufe 3"
        };
        if (i % 2 == 0)
            zeilen.Add($"{(28.9 + i % 4):0.00} m  BCAA  Seitlicher Anschluss einragend, 10 Uhr, Stufe 2");
        return string.Join(Environment.NewLine, zeilen);
    }

    /// <summary>
    /// Drei Beobachtungen mit echter Uhrlage und Stufe. <paramref name="offeneKiBefunde"/>
    /// legt fest, wie viele davon ein noch NICHT bestaetigter KI-Befund sind
    /// (Ai.Accepted = false); die uebrigen KI-Befunde gelten als bestaetigt. Damit zeigen
    /// Ampel, Uebersicht und Rohrring echte Werte statt Rueckfallwerte.
    /// </summary>
    static ProtocolDocument BaueProtokoll(string haltung, int offeneKiBefunde)
    {
        ProtocolEntry Eintrag(string code, string text, double meter, string von, string bis, string stufe, bool? kiBestaetigt)
        {
            var e = new ProtocolEntry
            {
                Code = code,
                Beschreibung = text,
                MeterStart = meter,
                MeterEnd = meter,
                Source = kiBestaetigt is null ? ProtocolEntrySource.Manual : ProtocolEntrySource.Ai,
                CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe }
            };
            e.CodeMeta.Parameters["Uhr_von"] = von;
            e.CodeMeta.Parameters["Uhr_bis"] = bis;
            if (kiBestaetigt is not null)
                e.Ai = new ProtocolEntryAiMeta
                {
                    SuggestedCode = code,
                    Confidence = 0.72,
                    Accepted = kiBestaetigt.Value,
                    Reason = "Kuenstlicher Vorschlag der Bedienprobe"
                };
            return e;
        }

        var current = new ProtocolRevision();
        current.Entries.Add(Eintrag("BAB", "Riss laengs", 4.2, "10", "2", "4", kiBestaetigt: offeneKiBefunde >= 1 ? false : true));
        current.Entries.Add(Eintrag("BAJ", "Verschobene Rohrverbindung", 21.3, "3", "3", "3", kiBestaetigt: offeneKiBefunde >= 2 ? false : true));
        current.Entries.Add(Eintrag("BBC", "Ablagerung", 12.8, "5", "7", "2", kiBestaetigt: null));
        var original = new ProtocolRevision();
        foreach (var e in current.Entries)
            original.Entries.Add(ProtocolEntryCloner.CloneLegacyProtocolEntry(e));
        return new ProtocolDocument { HaltungId = haltung, Original = original, Current = current };
    }

    /// <summary>
    /// Acht Schaechte mit Form und beiden Innenmassen (rund 600/600, oval 1100/900). Der
    /// sechste traegt bewusst keine Zustandsklasse, damit der Chip dort den gestrichelten
    /// Strich zeigt; zwei Drittel fuehren eine PDF, damit der Protokoll-Knopf sichtbar ist.
    /// </summary>
    static void BaueSchaechte(Project project)
    {
        var pdf = Path.Combine(Root, "medien", "protokoll.pdf");
        for (var i = 0; i < 8; i++)
        {
            var schacht = new SchachtRecord();
            void Set(string key, string value) => schacht.SetFieldValue(key, value, FieldSource.Manual, false);
            Set("Schachtnummer", (10001 + i).ToString());
            Set("Strasse", Strassen[i % Strassen.Length]);
            Set("Funktion", i % 3 == 0 ? "Normschacht" : i % 3 == 1 ? "Kontrollschacht" : "Einlaufschacht");
            Set("Material", Materialien[i % Materialien.Length]);
            if (i % 6 != 5)
                Set(FieldKeys.ConditionClass, (i % 5).ToString());
            if (i % 4 == 0)
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
            if (i % 3 != 2) Set(FieldKeys.PdfPath, pdf);
            project.SchaechteData.Add(schacht);
        }
    }

    /// <summary>
    /// Ein gespeichertes Spaltenlayout aus der Zeit vor der Fixwelle: jede Spalte
    /// linksbuendig und 72 px breit (Kopfbreite), die Kompakt-Migration bereits gelaufen.
    /// Nur so ist im Bild belegbar, dass die einmalige Migration greift.
    /// </summary>
    static DataPageLayoutSettings AltesLayout()
    {
        string[] felder =
        [
            FieldKeys.HoldingName, "Strasse", FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm,
            FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass
        ];

        var layout = new DataPageLayoutSettings
        {
            ActiveColumnView = "kompakt",
            NovaKompaktEinmalGesetzt = true,
            ZahlenRechtsEinmalGesetzt = false
        };

        for (var i = 0; i < felder.Length; i++)
        {
            layout.Columns.Add(new DataPageColumnLayout
            {
                FieldName = felder[i],
                DisplayIndex = i,
                WidthValue = 72d,
                WidthUnitType = "Pixel",
                HorizontalAlignment = "Left",
                VerticalAlignment = "Center",
                IsVisible = true
            });
        }

        return layout;
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
        // Etappe 2b: sichtbare Spalten und der aktive Ansichts-Chip gehoeren zum Nachweis.
        var sichtbareSpalten = grid?.Columns
            .Where(c => c.Visibility == Visibility.Visible)
            .Select(c => c.Header?.ToString() ?? "?")
            .ToArray() ?? [];
        var aktiverChip = Descendants(window)
            .OfType<System.Windows.Controls.Primitives.ToggleButton>()
            .Where(t => t.IsChecked == true && t.Tag is string)
            .Select(t => (string)t.Tag!)
            .FirstOrDefault();
        // Zellprobe der ersten sichtbaren Zeile: Ausrichtung und tatsaechliche Hoehe je Zelle.
        // Nur so ist "Zahlen rechts" und "hoechstens drei Zeilen" nachpruefbar statt geschaetzt.
        var ersteZeile = rows.FirstOrDefault();
        var zellen = ersteZeile is null ? [] : Descendants(ersteZeile).OfType<DataGridCell>()
            .Where(c => c.Column?.Visibility == Visibility.Visible)
            .Select(c =>
            {
                var text = Descendants(c).OfType<TextBlock>().FirstOrDefault();
                return new
                {
                    spalte = c.Column?.Header?.ToString() ?? "?",
                    zellBreite = Math.Round(c.ActualWidth, 1),
                    zellHoehe = Math.Round(c.ActualHeight, 1),
                    textBreite = text is null ? 0 : Math.Round(text.ActualWidth, 1),
                    textHoehe = text is null ? 0 : Math.Round(text.ActualHeight, 1),
                    ausrichtung = text?.TextAlignment.ToString(),
                    waagrecht = text?.HorizontalAlignment.ToString(),
                    schrift = (text?.FontFamily?.Source)
                };
            }).ToArray();
        var variantSuffix = string.IsNullOrEmpty(AppliedVariant) ? "" : "-" + AppliedVariant;
        File.WriteAllText(Path.Combine(Root, $"messung-{AppliedPage}{variantSuffix}-{AppliedTheme}.json"), JsonSerializer.Serialize(new
        {
            utc = DateTime.UtcNow,
            theme = AppliedTheme,
            seite = AppliedPage,
            variante = AppliedVariant,
            aktiveSpaltenansicht = aktiverChip,
            spaltenSichtbar = sichtbareSpalten.Length,
            spalten = sichtbareSpalten,
            // In "Alle Spalten" ist die Zeilenhoehe bewusst Auto (double.NaN); JSON kennt
            // dafuer keinen Wert, deshalb hier als null.
            zeilenhoehe = grid is null || double.IsNaN(grid.RowHeight) ? (double?)null : grid.RowHeight,
            datensaetze = grid?.Items.Count,
            zellprobe = zellen,
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
            // Fixwelle P2: Die nutzbare Hoehe der Tabelle gehoert zum Nachweis — ohne sie ist
            // "eine Zeile fehlt" nicht von "zwanzig Pixel fehlen" zu unterscheiden.
            viewportHoehe = viewport is null ? 0 : Math.Round(viewport.ActualHeight, 1),
            // Die tatsaechlichen Hoehen der ersten Zeilen: Die erste ist die gewaehlte und
            // deshalb um ihre Auswahlkontur hoeher als die uebrigen.
            zeilenHoehen = rows.Take(6).Select(r => Math.Round(r.ActualHeight, 1)).ToArray(),
            windowCount = System.Windows.Application.Current?.Windows.Count,
            selected = grid?.SelectedItem?.ToString()
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
