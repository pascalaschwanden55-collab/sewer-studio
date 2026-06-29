using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed class AppSettings : IAiStartupSettings
{
    private const int SaveDebounceMs = 750;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private static readonly object SaveSync = new();
    private static Timer? SaveDebounceTimer;
    private static PendingSettingsWrite? PendingWrite;

    public bool EnableDiagnostics { get; set; } = true;
    public string? PdfToTextPath { get; set; }
    public string? LastProjectPath { get; set; }

    // Basisverzeichnis fuer neu angelegte Projekte. Leer = beim ersten Anlegen
    // wird einmalig danach gefragt (Vorschlag D:\Projekt) und hier gespeichert.
    public string? ProjectsRootDirectory { get; set; }

    // Alle jemals geoeffneten Projekte (max 20, neueste zuerst)
    public List<string> RecentProjectPaths { get; set; } = new();

    /// <summary>Projekt-Pfad in RecentProjectPaths einfuegen (Duplikate vermeiden, max 20).</summary>
    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentProjectPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjectPaths.Insert(0, path);
        if (RecentProjectPaths.Count > 20)
            RecentProjectPaths.RemoveRange(20, RecentProjectPaths.Count - 20);
        LastProjectPath = path;
    }

    // Canonical source folder for video lookup/relink.
    public string? LastVideoSourceFolder { get; set; }

    // Last destination root used by distribution workflows.
    public string? LastDistributionTargetFolder { get; set; }

    // Legacy compatibility property (mirrors LastVideoSourceFolder).
    public string? LastVideoFolder { get; set; }
    public AutoSaveMode DataAutoSaveMode { get; set; } = AutoSaveMode.OnEachChange;
    public bool EnableRestorePoints { get; set; } = true;
    public string UiTheme { get; set; } = ThemeManager.Light;

    // Video player tuning
    public bool VideoHwDecoding { get; set; } = true;
    public bool VideoDropLateFrames { get; set; } = true;
    public bool VideoSkipFrames { get; set; } = true;
    public int VideoFileCachingMs { get; set; } = 3000;
    public int VideoNetworkCachingMs { get; set; } = 3000;
    public int VideoCodecThreads { get; set; } = 2;
    public string VideoOutput { get; set; } = "direct3d11";
    public DataPageLayoutSettings DataPageLayout { get; set; } = new();
    public DataPageLayoutSettings SchaechtePageLayout { get; set; } = new();

    // Haltungsansicht: per GridSplitter einstellbare Hoehe des "Primaere Schaeden"-Panels (in px).
    public double HaltungsansichtSchadenHeight { get; set; } = 240d;

    // Window position/size persistence
    public Dictionary<string, WindowBounds> WindowStates { get; set; } = new();

    // Multi-Monitor: Floating Grid Window
    public string? FloatingGridBounds { get; set; }
    public bool IsGridFloating { get; set; }
    public string? VsaCatalogSecXmlPath { get; set; }
    public string? VsaCatalogNodXmlPath { get; set; }

    // WinCan catalog directory for browsing and auto-discovery
    public string? WinCanCatalogDirectory { get; set; }

    // Eval-Set-Wurzel (eingefrorene Benchmark-Daten). Quelle fuer den Eval-Kontaminationsschutz:
    // Frames aus diesem Set werden hart aus dem KB-Index-Schreibpfad blockiert. Default = kanonischer
    // Projektpfad, ueberschreibbar; fehlt der Pfad/das Manifest, ist der Schutz leer (kein Blocken).
    public string EvalSetRoot { get; set; } = @"C:\KI_BRAIN\eval_set";

    // Amtlicher Abwasserkataster (SIA405-XTF) fuer die Haltungs-Zuordnung bei der Verteilung.
    // Schacht-Paar (auch vertauscht) wird hierueber der korrekten Haltung zugeordnet.
    // Fehlt die Datei, laeuft die Verteilung wie bisher (kein Kataster-Abgleich).
    public string AbwasserkatasterXtfPath { get; set; } = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf";

    // VSA Zustandklassifizierung v2: Shadow-Vergleich gegen Legacy-Engine.
    // Null bedeutet Default an.
    public bool? VsaClassificationShadowEnabled { get; set; }

    // VSA Zustandklassifizierung v2 produktiv nutzen. Null bedeutet Default an.
    public bool? VsaUseV2Engine { get; set; }

    // Multi-Model Pipeline Thresholds (overrides env vars if set)
    public bool? PipelineMultiModelEnabled { get; set; }
    public string? PipelineSidecarUrl { get; set; }
    public string? PipelineSidecarToken { get; set; }
    public string? PipelineMode { get; set; }
    public double? PipelineYoloConfidence { get; set; }
    public double? PipelineDinoBoxThreshold { get; set; }
    public double? PipelineDinoTextThreshold { get; set; }
    public int? PipelinePipeDiameterMm { get; set; }

    // AI / Ollama settings (overrides env vars if set)
    public bool AiStartOnProgramStart { get; set; }
    public bool? AiEnabled { get; set; }
    public string? AiOllamaUrl { get; set; }
    public string? AiVisionModel { get; set; }
    public string? AiTextModel { get; set; }
    public string? AiEmbedModel { get; set; }
    public int? AiOllamaTimeoutMin { get; set; }
    public string? AiOllamaKeepAlive { get; set; }
    public int? AiOllamaNumCtx { get; set; }
    public string? AiFfmpegPath { get; set; }

    // Hydraulik-Panel letzte Eingaben
    public HydraulikPanelSettings HydraulikPanel { get; set; } = new();

    public static string AppDataDir
        => AppDataPathResolver.Resolve(AppIdentity.ProductName);

    private static string LegacyAppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentity.LegacyLocalDataFolder);

    private static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    private static string LegacySettingsPath => Path.Combine(LegacyAppDataDir, "settings.json");
    private static string LogsDir => Path.Combine(AppDataDir, "logs");

    public static AppSettings Load()
    {
        try
        {
            MigrateLegacySettingsIfNeeded();

            Directory.CreateDirectory(AppDataDir);
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? throw new JsonException("settings.json enthaelt kein gueltiges Settings-Objekt.");
            return NormalizeAfterLoad(settings);
        }
        catch (JsonException ex)
        {
            TryQuarantineCorruptSettings(ex);
            return new AppSettings();
        }
        catch (Exception ex)
        {
            TryAppendSettingsLog("Settings konnten nicht geladen werden. Es werden Standardwerte verwendet.", ex);
            return new AppSettings();
        }
    }

    public void Save()
    {
        LastVideoFolder = LastVideoSourceFolder;
        var json = JsonSerializer.Serialize(this, JsonOptions);

        lock (SaveSync)
        {
            PendingWrite = new PendingSettingsWrite(json, EnableRestorePoints);

            if (SaveDebounceTimer is null)
            {
                SaveDebounceTimer = new Timer(
                    static _ => FlushPendingSaveFromTimer(),
                    null,
                    SaveDebounceMs,
                    Timeout.Infinite);
            }
            else
            {
                SaveDebounceTimer.Change(SaveDebounceMs, Timeout.Infinite);
            }
        }
    }

    public void SaveImmediate()
    {
        LastVideoFolder = LastVideoSourceFolder;
        var json = JsonSerializer.Serialize(this, JsonOptions);

        lock (SaveSync)
        {
            PendingWrite = null;
            SaveDebounceTimer?.Dispose();
            SaveDebounceTimer = null;
        }

        PersistSerializedState(json, EnableRestorePoints);
    }

    public static void FlushPendingSave()
    {
        PendingSettingsWrite? pending;
        lock (SaveSync)
        {
            pending = PendingWrite;
            PendingWrite = null;
            SaveDebounceTimer?.Dispose();
            SaveDebounceTimer = null;
        }

        if (pending is null)
            return;

        PersistSerializedState(pending.Json, pending.EnableRestorePoints);
    }

    private static void MigrateLegacySettingsIfNeeded()
        => SettingsMigrator.MigrateLegacyIfNeeded(SettingsPath, LegacySettingsPath, AppDataDir);

    private static AppSettings NormalizeAfterLoad(AppSettings settings)
    {
        settings.WindowStates ??= new Dictionary<string, WindowBounds>();
        settings.HydraulikPanel ??= new HydraulikPanelSettings();
        settings.DataPageLayout ??= new DataPageLayoutSettings();
        settings.DataPageLayout.Columns ??= new List<DataPageColumnLayout>();
        settings.SchaechtePageLayout ??= new DataPageLayoutSettings();
        settings.SchaechtePageLayout.Columns ??= new List<DataPageColumnLayout>();
        if (string.IsNullOrWhiteSpace(settings.LastVideoSourceFolder))
            settings.LastVideoSourceFolder = settings.LastVideoFolder;
        if (string.IsNullOrWhiteSpace(settings.LastVideoFolder))
            settings.LastVideoFolder = settings.LastVideoSourceFolder;
        settings.UiTheme = ThemeManager.NormalizeTheme(settings.UiTheme);
        return settings;
    }

    private static void FlushPendingSaveFromTimer()
    {
        try
        {
            FlushPendingSave();
        }
        catch (Exception ex)
        {
            TryAppendSettingsLog("Debounced Settings-Save ist fehlgeschlagen.", ex);
        }
    }

    private static void PersistSerializedState(string json, bool enableRestorePoints)
        => SettingsStore.Persist(json, SettingsPath, AppDataDir, enableRestorePoints);

    private static void TryQuarantineCorruptSettings(Exception ex)
        => SettingsQuarantine.TryMoveToQuarantine(SettingsPath, AppDataDir, ex, TryAppendSettingsLog);

    private static void TryAppendSettingsLog(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(LogsDir);
            var logPath = Path.Combine(LogsDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            var builder = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [Settings] ")
                .AppendLine(message);

            if (ex is not null)
                builder.AppendLine(ex.ToString());

            File.AppendAllText(logPath, builder.ToString());
        }
        catch
        {
            // logging failures must never break settings recovery
        }
    }

    private sealed record PendingSettingsWrite(string Json, bool EnableRestorePoints);
}

public sealed class DataPageLayoutSettings
{
    public double GridMinRowHeight { get; set; } = 38d;
    public double GridZoom { get; set; } = 1.0d;
    public bool IsColumnReorderEnabled { get; set; }
    public List<DataPageColumnLayout> Columns { get; set; } = new();
}

public sealed class DataPageColumnLayout
{
    public string FieldName { get; set; } = "";
    public int DisplayIndex { get; set; }
    public double WidthValue { get; set; } = 120d;
    public string WidthUnitType { get; set; } = "Pixel";
    public string HorizontalAlignment { get; set; } = "Left";
    public string VerticalAlignment { get; set; } = "Center";
}

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}

public sealed class HydraulikPanelSettings
{
    public double Dn { get; set; } = 300;
    public string MaterialKey { get; set; } = "Beton";
    public bool IsNeuzustand { get; set; }
    public double Gefaelle { get; set; } = 5;
    public bool IsGefaellePercent { get; set; }
    public double Wasserstand { get; set; } = 90;
    public bool IsMischRegen { get; set; } = true;
    public double Temperatur { get; set; } = 10;
}
