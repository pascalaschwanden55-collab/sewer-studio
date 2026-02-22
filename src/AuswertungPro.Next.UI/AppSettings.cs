using System.Text.Json;
using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public sealed class AppSettings
{
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
    public int VideoCodecThreads { get; set; } = 4;
    public string VideoOutput { get; set; } = "direct3d11";
    public DataPageLayoutSettings DataPageLayout { get; set; } = new();
    public DataPageLayoutSettings SchaechtePageLayout { get; set; } = new();
    public string? VsaCatalogSecXmlPath { get; set; }
    public string? VsaCatalogNodXmlPath { get; set; }

    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentity.ProductName);

    private static string LegacyAppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppIdentity.LegacyLocalDataFolder);

    private static string SettingsPath => Path.Combine(AppDataDir, "settings.json");
    private static string LegacySettingsPath => Path.Combine(LegacyAppDataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            MigrateLegacySettingsIfNeeded();

            Directory.CreateDirectory(AppDataDir);
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new AppSettings();

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
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        LastVideoFolder = LastVideoSourceFolder;

        Directory.CreateDirectory(AppDataDir);
        if (EnableRestorePoints)
        {
            RestorePointService.TryCreate(
                sourceFilePath: SettingsPath,
                restoreRoot: RestorePointService.SettingsRestoreRoot,
                scopeName: "settings");
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
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
}

public sealed class DataPageLayoutSettings
{
    public double GridMinRowHeight { get; set; } = 38d;
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
