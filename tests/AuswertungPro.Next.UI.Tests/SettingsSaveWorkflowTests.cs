using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsSaveWorkflowTests
{
    [Fact]
    public void Save_applies_normalized_values_to_settings_diagnostics_and_save_action()
    {
        var settings = new AppSettings();
        var diagnostics = new DiagnosticsOptions();
        var calls = new List<string>();

        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(
            Settings: settings,
            Diagnostics: diagnostics,
            Values: new SettingsSaveValues(
                EnableDiagnostics: false,
                PdfToTextPath: @"C:\Tools\pdftotext.exe",
                ProjectPath: @"  D:\Projekte\Uri  ",
                ProjectsRootDirectory: @"  D:\Projekte  ",
                AbwasserkatasterXtfPath: @"  D:\QGIS_V4.03\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf  ",
                VideoFolder: @"D:\Videos",
                KantonUriXtfDirectory: @"  D:\Uri\XTF  ",
                DataAutoSaveMode: (AutoSaveMode)999,
                EnableRestorePoints: false,
                VideoHwDecoding: false,
                VideoDropLateFrames: false,
                VideoSkipFrames: false,
                VideoFileCachingMs: 20,
                VideoNetworkCachingMs: 20000,
                VideoCodecThreads: 99,
                VideoOutput: " DIRECT3D9 ",
                UiTheme: "dark",
                StartAiOnProgramStart: true,
                PipelineYoloConfidence: 1.2,
                PipelineDinoBoxThreshold: 0.45,
                PipelineDinoTextThreshold: -0.1),
            SaveSettings: () => calls.Add("save")));

        Assert.False(settings.EnableDiagnostics);
        Assert.Equal(@"C:\Tools\pdftotext.exe", settings.PdfToTextPath);
        Assert.Equal(@"D:\Projekte\Uri.json", settings.LastProjectPath);
        Assert.Equal(@"D:\Projekte", settings.ProjectsRootDirectory);
        Assert.Equal(@"D:\QGIS_V4.03\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf", settings.AbwasserkatasterXtfPath);
        Assert.Equal(@"D:\Videos", settings.LastVideoSourceFolder);
        Assert.Equal(@"D:\Videos", settings.LastVideoFolder);
        Assert.Equal(@"D:\Uri\XTF", settings.KantonUriXtfDirectory);
        Assert.Equal(AutoSaveMode.OnEachChange, settings.DataAutoSaveMode);
        Assert.False(settings.EnableRestorePoints);
        Assert.False(settings.VideoHwDecoding);
        Assert.False(settings.VideoDropLateFrames);
        Assert.False(settings.VideoSkipFrames);
        Assert.Equal(100, settings.VideoFileCachingMs);
        Assert.Equal(10000, settings.VideoNetworkCachingMs);
        Assert.Equal(16, settings.VideoCodecThreads);
        Assert.Equal("direct3d9", settings.VideoOutput);
        Assert.Equal(ThemeManager.Dark, settings.UiTheme);
        Assert.True(settings.AiStartOnProgramStart);
        Assert.Equal(1.0, settings.PipelineYoloConfidence);
        Assert.Equal(0.45, settings.PipelineDinoBoxThreshold);
        Assert.Equal(0.0, settings.PipelineDinoTextThreshold);
        Assert.False(diagnostics.EnableDiagnostics);
        Assert.Equal(@"C:\Tools\pdftotext.exe", diagnostics.ExplicitPdfToTextPath);
        Assert.Equal(["save"], calls);
    }

    [Fact]
    public void Save_maps_blank_optional_paths_to_null_or_empty_and_defaults_video_output()
    {
        var settings = new AppSettings
        {
            LastProjectPath = @"D:\Alt.json",
            ProjectsRootDirectory = @"D:\Alt",
            AbwasserkatasterXtfPath = @"D:\Alt\Kataster.xtf",
            KantonUriXtfDirectory = @"D:\Alt\XTF",
            VideoOutput = "direct3d9"
        };
        var diagnostics = new DiagnosticsOptions();

        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(
            Settings: settings,
            Diagnostics: diagnostics,
            Values: MinimalValues with
            {
                ProjectPath = " ",
                ProjectsRootDirectory = " ",
                AbwasserkatasterXtfPath = " ",
                KantonUriXtfDirectory = " ",
                VideoOutput = "unbekannt",
                UiTheme = "hell"
            },
            SaveSettings: () => { }));

        Assert.Null(settings.LastProjectPath);
        Assert.Null(settings.ProjectsRootDirectory);
        Assert.Equal("", settings.AbwasserkatasterXtfPath);
        Assert.Equal("", settings.KantonUriXtfDirectory);
        Assert.Equal("direct3d11", settings.VideoOutput);
        Assert.Equal(ThemeManager.Light, settings.UiTheme);
    }

    [Fact]
    public void Save_resolves_kataster_xtf_from_kanton_uri_directory_when_file_path_is_stale()
    {
        using var dir = new TempDirectory();
        var expected = Path.Combine(dir.Path, "Abwasserkataster_Uri_korrigiert.xtf");
        File.WriteAllText(expected, "<TRANSFER />");
        var settings = new AppSettings();
        var diagnostics = new DiagnosticsOptions();

        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(
            Settings: settings,
            Diagnostics: diagnostics,
            Values: MinimalValues with
            {
                AbwasserkatasterXtfPath = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf",
                KantonUriXtfDirectory = dir.Path
            },
            SaveSettings: () => { }));

        Assert.Equal(expected, settings.AbwasserkatasterXtfPath);
        Assert.Equal(dir.Path, settings.KantonUriXtfDirectory);
    }

    [Fact]
    public void Der_Schalter_fuer_KI_Vorschlaege_im_Codiermodus_wird_gespeichert()
    {
        var settings = new AppSettings();
        Assert.True(settings.CodingSuggestionsEnabled); // Standard: ein

        var values = MinimalValues with { CodingSuggestionsEnabled = false };
        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(settings, new DiagnosticsOptions(), values, () => { }));

        Assert.False(settings.CodingSuggestionsEnabled);
    }

    private static SettingsSaveValues MinimalValues => new(
        EnableDiagnostics: true,
        PdfToTextPath: null,
        ProjectPath: null,
        ProjectsRootDirectory: null,
        AbwasserkatasterXtfPath: null,
        VideoFolder: null,
        KantonUriXtfDirectory: null,
        DataAutoSaveMode: AutoSaveMode.OnEachChange,
        EnableRestorePoints: true,
        VideoHwDecoding: true,
        VideoDropLateFrames: true,
        VideoSkipFrames: true,
        VideoFileCachingMs: 3000,
        VideoNetworkCachingMs: 3000,
        VideoCodecThreads: 2,
        VideoOutput: "direct3d11",
        UiTheme: ThemeManager.Light,
        StartAiOnProgramStart: false,
        PipelineYoloConfidence: 0.25,
        PipelineDinoBoxThreshold: 0.25,
        PipelineDinoTextThreshold: 0.20);

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
