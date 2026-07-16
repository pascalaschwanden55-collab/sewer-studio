using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Hydraulik;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed class AppSettings : IAiStartupSettings, IPlayerControlSettingsStore
{
    private const int SaveDebounceMs = 750;
    public const string DefaultQgisExportDirectory = @"D:\QGIS_V4.03\Export_Sewer_Studio";
    public const string DefaultAbwasserkatasterXtfPath = DefaultQgisExportDirectory + @"\Abwasserkataster_Uri_korrigiert.xtf";
    public const string DefaultKantonUriXtfDirectory = DefaultQgisExportDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private static readonly object SaveSync = new();
    private static Timer? SaveDebounceTimer;
    private static PendingSettingsWrite? PendingWrite;
    private ISettingsFileStore _settingsFileStore = SettingsStore.CreateDefault();

    public bool EnableDiagnostics { get; set; } = true;
    public string? PdfToTextPath { get; set; }
    public string? LastProjectPath { get; set; }

    // Basisverzeichnis fuer neu angelegte Projekte. Leer = beim ersten Anlegen
    // wird einmalig danach gefragt (Vorschlag D:\Projekt) und hier gespeichert.
    public string? ProjectsRootDirectory { get; set; }

    // Alle jemals geoeffneten Projekte (max 20, neueste zuerst)
    public List<string> RecentProjectPaths { get; set; } = new();

    // Aus der Projektuebersicht ausgeblendete Projekte. "Loeschen" in der Uebersicht
    // entfernt ein Projekt nur aus der Liste — die Dateien im Ordner bleiben erhalten.
    public List<string> HiddenProjectPaths { get; set; } = new();
    public bool OverviewProjectListCollapsed { get; set; }

    /// <summary>Projekt-Pfad in RecentProjectPaths einfuegen (Duplikate vermeiden, max 20).</summary>
    public void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        // Ein wieder geoeffnetes Projekt ist nicht mehr ausgeblendet.
        HiddenProjectPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjectPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentProjectPaths.Insert(0, path);
        if (RecentProjectPaths.Count > 20)
            RecentProjectPaths.RemoveRange(20, RecentProjectPaths.Count - 20);
        LastProjectPath = path;
    }

    /// <summary>
    /// Blendet ein Projekt aus der Uebersicht aus, OHNE Dateien zu loeschen:
    /// aus der Merkliste nehmen und in die Ausblend-Liste aufnehmen.
    /// </summary>
    public void HideProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RecentProjectPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        HiddenProjectPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        HiddenProjectPaths.Add(path);
        if (string.Equals(LastProjectPath, path, StringComparison.OrdinalIgnoreCase))
            LastProjectPath = null;
    }

    // Canonical source folder for video lookup/relink.
    public string? LastVideoSourceFolder { get; set; }

    // Last destination root used by distribution workflows.
    public string? LastDistributionTargetFolder { get; set; }

    // Konfigurierbare Ziel-Ablage je Verteil-/Export-Typ: Ziel-Wurzel + 3 Namens-/Ordner-Ebenen
    // (Ordner/Unterordner/Datei) als Platzhalter-Muster. Leere Ordner-Ebenen entfallen.
    // Die zwei Muster sind optionale Ueberordner. Der feste Objektordner bleibt im Verteiler erhalten.
    public DistributionTargetConfig HaltungDistribution { get; set; } = new() { DateiPattern = "{Datum}_{Haltung}" };
    public DistributionTargetConfig SchachtDistribution { get; set; } = new() { DateiPattern = "{Datum}_{Schachtnummer}" };
    public DistributionTargetConfig DichtheitDistribution { get; set; } = new() { DateiPattern = "{Datum}_{Haltung}_DP" };

    // Excel-Export: ein gemeinsamer Ziel-Ordner, aber getrennte Dateinamen.
    public string? ExcelExportRoot { get; set; }
    // Ein abweichender alter Schacht-Zielordner bleibt bei der Zusammenfuehrung nachvollziehbar.
    public string? LegacySchachtExportRoot { get; set; }
    public DistributionTargetConfig HaltungExport { get; set; } = new() { DateiPattern = "Haltungen" };
    public DistributionTargetConfig SchachtExport { get; set; } = new() { DateiPattern = "Schaechte" };

    // Legacy compatibility property (mirrors LastVideoSourceFolder).
    public string? LastVideoFolder { get; set; }
    public AutoSaveMode DataAutoSaveMode { get; set; } = AutoSaveMode.OnEachChange;
    public bool EnableRestorePoints { get; set; } = true;
    public string UiTheme { get; set; } = ThemeManager.Light;

    // Darstellung: true = dauerhafte Puls-/Leuchteffekte aus. Ohne Eintrag in der settings.json
    // gilt false, und MotionSettings folgt dann der Windows-Systemeinstellung.
    public bool ReduceMotion { get; set; }

    // Video player tuning
    public bool VideoHwDecoding { get; set; } = true;
    public bool VideoDropLateFrames { get; set; } = true;
    public bool VideoSkipFrames { get; set; } = true;
    public int VideoFileCachingMs { get; set; } = 3000;
    public int VideoNetworkCachingMs { get; set; } = 3000;
    public int VideoCodecThreads { get; set; } = 2;
    public string VideoOutput { get; set; } = "direct3d11";
    public int PlayerVolume { get; set; } = 80;
    public bool PlayerMuted { get; set; }
    public double PlayerOverlayOpacity { get; set; } = 1d;
    public DataPageLayoutSettings DataPageLayout { get; set; } = new();
    public DataPageLayoutSettings SchaechtePageLayout { get; set; } = new();

    // Haltungsansicht: per GridSplitter einstellbare Hoehe des "Primaere Schaeden"-Panels (in px).
    public double HaltungsansichtSchadenHeight { get; set; } = 240d;

    // Schachtansicht: per GridSplitter einstellbare Hoehe des "Schaeden"-Panels (in px).
    public double SchachtansichtSchadenHeight { get; set; } = 240d;

    // Foto-Galerie: Kachelbreite im Haltungs-/Schachtdetail.
    public double PhotoGalleryTileSize { get; set; } = 124d;

    // Window position/size persistence
    public Dictionary<string, WindowBounds> WindowStates { get; set; } = new();

    // Pro-Seite/-Fenster einstellbare Anpassungen (Spalten, Panelgroessen, gespeicherte Ansichten),
    // keyed by ViewKey (z.B. "BuilderPage"). Erbt die komplette gehaertete settings.json-Persistenz.
    public Dictionary<string, ViewCustomization> ViewCustomizations { get; set; } = new();

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

    // AP-06: Zuletzt beim Start tatsaechlich genutzter Wissensdatenbank-Ordner und dessen Sample-Zahl.
    // Dienen NUR der Abweichungs-Warnung (KnowledgeRootGuard) — nicht der Pfad-Aufloesung. Weicht der
    // Ordner beim naechsten Start ab oder ist die DB ploetzlich leer, warnt die App (Split-Brain-Schutz).
    public string? LastKnownKnowledgeRoot { get; set; }
    public int? LastKnownKnowledgeSampleCount { get; set; }
    // Dauerhaft gewaehlter Wissensordner. Die Umgebungsvariable darf ihn fuer einen
    // Start uebersteuern, ersetzt diesen gespeicherten Wert aber nicht.
    public string? KnowledgeRootPath { get; set; }

    internal bool MigrateLegacyKnowledgeRootPath()
    {
        if (!string.IsNullOrWhiteSpace(KnowledgeRootPath)
            || string.IsNullOrWhiteSpace(LastKnownKnowledgeRoot))
            return false;

        // Aeltere settings.json kannten nur LastKnownKnowledgeRoot. Diesen Wert
        // uebernehmen wir einmalig, damit ein verlorener Env-Override keine leere KB oeffnet.
        KnowledgeRootPath = LastKnownKnowledgeRoot.Trim();
        return true;
    }

    /// <summary>
    /// Uebernimmt bei alten Einstellungen einen der bisher getrennten Excel-Zielordner.
    /// Der Haltungs-Ordner hat Vorrang; fehlt er, wird der Schacht-Ordner verwendet.
    /// </summary>
    internal bool MigrateLegacyExcelExportRoot()
    {
        EnsureExcelExportConfigs();
        NormalizeExcelExportFilePatterns();

        var hadSharedRoot = !string.IsNullOrWhiteSpace(ExcelExportRoot);
        if (!hadSharedRoot
            && !string.IsNullOrWhiteSpace(HaltungExport.Root)
            && !string.IsNullOrWhiteSpace(SchachtExport.Root)
            && !string.Equals(
                HaltungExport.Root.Trim(),
                SchachtExport.Root.Trim(),
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(LegacySchachtExportRoot))
        {
            LegacySchachtExportRoot = SchachtExport.Root.Trim();
        }

        var candidate = hadSharedRoot
            ? ExcelExportRoot
            : !string.IsNullOrWhiteSpace(HaltungExport.Root)
                ? HaltungExport.Root
                : SchachtExport.Root;

        SetExcelExportRoot(candidate);
        return !hadSharedRoot && !string.IsNullOrWhiteSpace(ExcelExportRoot);
    }

    /// <summary>
    /// Garantiert zwei nicht-leere, verschiedene Excel-Dateinamen. So kann der zweite
    /// Export die erste Datei im gemeinsamen Zielordner nicht versehentlich ersetzen.
    /// </summary>
    internal bool NormalizeExcelExportFilePatterns()
    {
        EnsureExcelExportConfigs();
        var changed = false;

        if (string.IsNullOrWhiteSpace(HaltungExport.DateiPattern))
        {
            HaltungExport.DateiPattern = "Haltungen";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(SchachtExport.DateiPattern))
        {
            SchachtExport.DateiPattern = "Schaechte";
            changed = true;
        }

        if (string.Equals(
                HaltungExport.DateiPattern.Trim(),
                SchachtExport.DateiPattern.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(HaltungExport.DateiPattern.Trim(), "Schaechte", StringComparison.OrdinalIgnoreCase))
                HaltungExport.DateiPattern = "Haltungen";
            else
                SchachtExport.DateiPattern = "Schaechte";
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Setzt den gemeinsamen Excel-Zielordner und spiegelt ihn fuer alte Programmstaende
    /// weiterhin in die beiden bisherigen Root-Felder.
    /// </summary>
    internal string? SetExcelExportRoot(string? value)
    {
        EnsureExcelExportConfigs();

        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        ExcelExportRoot = normalized;
        HaltungExport.Root = normalized;
        SchachtExport.Root = normalized;
        return normalized;
    }

    private void EnsureExcelExportConfigs()
    {
        HaltungExport ??= new DistributionTargetConfig { DateiPattern = "Haltungen" };
        SchachtExport ??= new DistributionTargetConfig { DateiPattern = "Schaechte" };
    }

    internal void RecordKnowledgeRootStart(
        string activeRoot,
        int? sampleCount,
        KnowledgeBasePaths.RootSource source)
    {
        LastKnownKnowledgeRoot = activeRoot;
        if (sampleCount.HasValue)
            LastKnownKnowledgeSampleCount = sampleCount.Value;

        // Der normale Fallback wird nach dem ersten Start dauerhaft festgehalten.
        // Ein Env-Override bleibt dagegen absichtlich nur fuer diesen Start aktiv.
        if (source == KnowledgeBasePaths.RootSource.DefaultFallback)
            KnowledgeRootPath = activeRoot;
    }

    // Amtlicher Abwasserkataster (SIA405-XTF) fuer die Haltungs-Zuordnung bei der Verteilung.
    // Schacht-Paar (auch vertauscht) wird hierueber der korrekten Haltung zugeordnet.
    // Fehlt die Datei, laeuft die Verteilung wie bisher (kein Kataster-Abgleich).
    public string AbwasserkatasterXtfPath { get; set; } = DefaultAbwasserkatasterXtfPath;

    // Vollstaendiger XTF-Datenbestand Kanton Uri (Leitungen und Schaechte).
    public string KantonUriXtfDirectory { get; set; } = DefaultKantonUriXtfDirectory;

    // Lokale QGIS-XYZ-Kacheln fuer die Kartenansicht. Fehlt der Ordner, bleibt es beim WMS.
    public string QgisTilesPath { get; set; } = DefaultQgisExportDirectory + @"\tiles_test";

    // Offline-Hintergrundkarten: Basisordner im Programmordner mit den Unterordnern
    // "satellit" (SWISSIMAGE, JPEG) und "av" (AV-Karte farbig/Grundbuch, PNG), Kanton Uri z18.
    // Standard-Hintergrund der App-Karte; fehlt ein Ordner, wird stattdessen OSM online genutzt.
    // In den Einstellungen aenderbar.
    public string OfflineBasemapPath { get; set; } = @"c:\Sewer-Studio_KI_4.5\basemap_tiles";

    // VSA Zustandklassifizierung v2: Shadow-Vergleich gegen Legacy-Engine.
    // Null bedeutet Default an.
    public bool? VsaClassificationShadowEnabled { get; set; }

    // VSA Zustandklassifizierung v2 produktiv nutzen. Null bedeutet Default an.
    public bool? VsaUseV2Engine { get; set; }

    // Multi-Model Pipeline Thresholds (overrides env vars if set)
    public bool? PipelineMultiModelEnabled { get; set; }
    public string? PipelineSidecarUrl { get; set; }
    [JsonConverter(typeof(DpapiProtectedStringJsonConverter))]
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

    // Komplette Datensicherung (PC-Ausfall-Schutz)
    public DateTime? LastFullBackupUtc { get; set; }
    public string? LastFullBackupPath { get; set; }
    public long? LastFullBackupSizeBytes { get; set; }
    public bool FullBackupIncludeProjectVideos { get; set; }

    public static string AppDataDir
        => AppDataPathResolver.Resolve(AppIdentity.ProductName);

    private static string LegacyAppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentity.LegacyLocalDataFolder);

    private static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    private static string LegacySettingsPath => Path.Combine(LegacyAppDataDir, "settings.json");
    private static string LogsDir => Path.Combine(AppDataDir, "logs");

    public static AppSettings Load()
        => Load(
            SettingsQuarantine.DefaultStore,
            SettingsMigrator.DefaultService);

    internal static AppSettings Load(ISettingsQuarantineStore settingsQuarantine)
        => Load(settingsQuarantine, SettingsMigrator.DefaultService);

    internal static AppSettings Load(
        ISettingsQuarantineStore settingsQuarantine,
        ISettingsMigrationService settingsMigration)
    {
        ArgumentNullException.ThrowIfNull(settingsQuarantine);
        ArgumentNullException.ThrowIfNull(settingsMigration);

        try
        {
            MigrateLegacySettingsIfNeeded(settingsMigration);

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
            TryQuarantineCorruptSettings(ex, settingsQuarantine);
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
        MigrateLegacyExcelExportRoot();
        var json = JsonSerializer.Serialize(this, JsonOptions);

        lock (SaveSync)
        {
            PendingWrite = new PendingSettingsWrite(
                json,
                EnableRestorePoints,
                _settingsFileStore);

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
        MigrateLegacyExcelExportRoot();
        var json = JsonSerializer.Serialize(this, JsonOptions);

        lock (SaveSync)
        {
            PendingWrite = null;
            SaveDebounceTimer?.Dispose();
            SaveDebounceTimer = null;
        }

        PersistSerializedState(json, EnableRestorePoints, _settingsFileStore);
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

        PersistSerializedState(
            pending.Json,
            pending.EnableRestorePoints,
            pending.SettingsFileStore);
    }

    private static void MigrateLegacySettingsIfNeeded(
        ISettingsMigrationService settingsMigration)
    {
        var migrationResult = settingsMigration.MigrateLegacyIfNeeded(
            SettingsPath,
            LegacySettingsPath,
            AppDataDir);
        if (migrationResult.Error is not null)
        {
            TryAppendSettingsLog(
                "Alte Einstellungen konnten nicht uebernommen werden.",
                migrationResult.Error);
        }
    }

    private static AppSettings NormalizeAfterLoad(AppSettings settings)
    {
        settings.MigrateLegacyKnowledgeRootPath();
        settings.HaltungDistribution ??= new DistributionTargetConfig { DateiPattern = "{Datum}_{Haltung}" };
        settings.SchachtDistribution ??= new DistributionTargetConfig { DateiPattern = "{Datum}_{Schachtnummer}" };
        settings.DichtheitDistribution ??= new DistributionTargetConfig { DateiPattern = "{Datum}_{Haltung}_DP" };
        if (string.Equals(
                settings.DichtheitDistribution.DateiPattern,
                "{Datum}_{Schachtnummer}",
                StringComparison.Ordinal))
        {
            settings.DichtheitDistribution.DateiPattern = "{Datum}_{Haltung}_DP";
        }
        settings.MigrateLegacyExcelExportRoot();
        settings.WindowStates ??= new Dictionary<string, WindowBounds>();
        settings.ViewCustomizations ??= new Dictionary<string, ViewCustomization>();
        settings.HydraulikPanel ??= new HydraulikPanelSettings();
        settings.DataPageLayout ??= new DataPageLayoutSettings();
        settings.DataPageLayout.Columns ??= new List<DataPageColumnLayout>();
        settings.SchaechtePageLayout ??= new DataPageLayoutSettings();
        settings.SchaechtePageLayout.Columns ??= new List<DataPageColumnLayout>();
        if (string.IsNullOrWhiteSpace(settings.LastVideoSourceFolder))
            settings.LastVideoSourceFolder = settings.LastVideoFolder;
        if (string.IsNullOrWhiteSpace(settings.LastVideoFolder))
            settings.LastVideoFolder = settings.LastVideoSourceFolder;
        settings.AbwasserkatasterXtfPath ??= DefaultAbwasserkatasterXtfPath;
        settings.KantonUriXtfDirectory ??= DefaultKantonUriXtfDirectory;
        settings.UiTheme = ThemeManager.NormalizeTheme(settings.UiTheme);
        settings.PhotoGalleryTileSize = Math.Clamp(settings.PhotoGalleryTileSize, 80d, 260d);
        settings.PlayerVolume = Math.Clamp(settings.PlayerVolume, 0, 100);
        settings.PlayerOverlayOpacity = Math.Clamp(settings.PlayerOverlayOpacity, 0.35d, 1d);
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

    internal void UseSettingsFileStore(ISettingsFileStore settingsFileStore)
        => _settingsFileStore = settingsFileStore
            ?? throw new ArgumentNullException(nameof(settingsFileStore));

    private static void PersistSerializedState(
        string json,
        bool enableRestorePoints,
        ISettingsFileStore settingsFileStore)
        => settingsFileStore.Persist(json, SettingsPath, AppDataDir, enableRestorePoints);

    private static void TryQuarantineCorruptSettings(
        Exception ex,
        ISettingsQuarantineStore settingsQuarantine)
        => settingsQuarantine.TryMoveToQuarantine(
            SettingsPath,
            AppDataDir,
            ex,
            TryAppendSettingsLog);

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

    private sealed record PendingSettingsWrite(
        string Json,
        bool EnableRestorePoints,
        ISettingsFileStore SettingsFileStore);
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

    // Spalte sichtbar? Default true = verhaltensneutral fuer alle bestehenden Layouts
    // (aeltere settings.json ohne dieses Feld deserialisieren zu true -> nichts wird versteckt).
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Pro-Seite/-Fenster gespeicherte Anpassungen. Alle Felder tolerant/defaultbar,
/// damit unbekannte oder leere Keys nie werfen.
/// </summary>
public sealed class ViewCustomization
{
    // Grid-Layouts je GridKey: traegt Spalten (inkl. Sichtbarkeit) UND Zoom/Zeilenhoehe.
    public Dictionary<string, DataPageLayoutSettings> Grids { get; set; } = new();

    // Panelgroessen je SplitterKey (Pixel).
    public Dictionary<string, double> SplitterSizes { get; set; } = new();

    // Benannte Ansichten (Filter + Spalten + Sortierung).
    public List<SavedView> SavedViews { get; set; } = new();
}

/// <summary>
/// Eine benannte Ansicht. Der Filter wird als JSON-String gehalten, damit AppSettings
/// von seitenspezifischen Filter-Typen (z.B. BuilderPageFilterCriteria) entkoppelt bleibt.
/// </summary>
public sealed class SavedView
{
    public string Name { get; set; } = "";
    public string? FilterJson { get; set; }
    public DataPageLayoutSettings? Columns { get; set; }
    public string? SortFieldName { get; set; }
    public string? SortDirection { get; set; }
}

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}
