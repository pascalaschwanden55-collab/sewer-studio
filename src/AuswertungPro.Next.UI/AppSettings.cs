using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed class AppSettings
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

    // Canonical source folder for video lookup/relink.
    public string? LastVideoSourceFolder { get; set; }

    // Last destination root used by distribution workflows.
    public string? LastDistributionTargetFolder { get; set; }

    // Legacy compatibility property (mirrors LastVideoSourceFolder).
    public string? LastVideoFolder { get; set; }
    public AutoSaveMode DataAutoSaveMode { get; set; } = AutoSaveMode.OnEachChange;
    public bool EnableRestorePoints { get; set; } = true;

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

    // Window position/size persistence
    public Dictionary<string, WindowBounds> WindowStates { get; set; } = new();

    // Multi-Monitor: Floating Grid Window
    public string? FloatingGridBounds { get; set; }
    public bool IsGridFloating { get; set; }
    public string? VsaCatalogSecXmlPath { get; set; }
    public string? VsaCatalogNodXmlPath { get; set; }

    // WinCan catalog directory for browsing and auto-discovery
    public string? WinCanCatalogDirectory { get; set; }

    // Multi-Model Pipeline Thresholds (overrides env vars if set)
    public bool? PipelineMultiModelEnabled { get; set; }
    public string? PipelineSidecarUrl { get; set; }
    public string? PipelineMode { get; set; }
    public double? PipelineYoloConfidence { get; set; }
    public double? PipelineDinoBoxThreshold { get; set; }
    public double? PipelineDinoTextThreshold { get; set; }
    public int? PipelinePipeDiameterMm { get; set; }

    // AI / Ollama settings (overrides env vars if set)
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

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentity.ProductName);

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
    {
        try
        {
            if (File.Exists(SettingsPath))
                return;

            if (!File.Exists(LegacySettingsPath))
                return;

            Directory.CreateDirectory(AppDataDir);
            File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
        }
        catch
        {
            // ignore migration errors
        }
    }

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
    {
        string? tempPath = null;

        try
        {
            Directory.CreateDirectory(AppDataDir);
            if (enableRestorePoints)
            {
                RestorePointService.TryCreate(
                    sourceFilePath: SettingsPath,
                    restoreRoot: RestorePointService.SettingsRestoreRoot,
                    scopeName: "settings");
            }

            tempPath = Path.Combine(AppDataDir, $".{Path.GetFileName(SettingsPath)}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempPath, json);

            if (File.Exists(SettingsPath))
            {
                var backupPath = SettingsPath + ".bak";
                try
                {
                    File.Replace(tempPath, SettingsPath, backupPath, ignoreMetadataErrors: true);
                }
                catch (Exception ex) when (ex is PlatformNotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
                {
                    File.Copy(SettingsPath, backupPath, overwrite: true);
                    File.Move(tempPath, SettingsPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, SettingsPath, overwrite: false);
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
            }
        }
    }

    private static void TryQuarantineCorruptSettings(Exception ex)
    {
        string? quarantinePath = null;

        try
        {
            if (!File.Exists(SettingsPath))
            {
                TryAppendSettingsLog("Settings-Load meldete korrupte Daten, aber settings.json wurde nicht gefunden.", ex);
                return;
            }

            Directory.CreateDirectory(AppDataDir);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            quarantinePath = Path.Combine(AppDataDir, $"settings.corrupt-{stamp}.json");

            File.Move(SettingsPath, quarantinePath, overwrite: false);
            TryAppendSettingsLog($"Korrupte settings.json wurde nach '{quarantinePath}' verschoben.", ex);
        }
        catch (Exception moveEx)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;

                quarantinePath ??= Path.Combine(AppDataDir, $"settings.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");
                File.Copy(SettingsPath, quarantinePath, overwrite: false);

                try
                {
                    File.Delete(SettingsPath);
                }
                catch
                {
                    // best effort delete; if this fails, startup still continues with defaults
                }

                TryAppendSettingsLog(
                    $"Korrupte settings.json wurde nach fehlgeschlagenem Move nach '{quarantinePath}' kopiert.",
                    new AggregateException(ex, moveEx));
            }
            catch (Exception copyEx)
            {
                TryAppendSettingsLog(
                    "Korrupte settings.json konnte nicht in Quarantaene verschoben werden. Es werden Standardwerte verwendet.",
                    new AggregateException(ex, moveEx, copyEx));
            }
        }
    }

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
