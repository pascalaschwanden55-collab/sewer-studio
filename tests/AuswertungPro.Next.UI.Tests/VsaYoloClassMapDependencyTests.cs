using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaYoloClassMapDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_die_projektbezogene_Yolo_Klassenkarte()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.VsaYoloClasses,
            services.GetService(typeof(IVsaYoloClassMapStore)));
    }

    [Fact]
    public void Statische_Yolo_Fassade_ist_unveraenderbar()
    {
        var before = VsaYoloClassMap.Current;
        var use = typeof(VsaYoloClassMap).GetMethod(nameof(VsaYoloClassMap.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [new RecordingClassMap()]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, VsaYoloClassMap.Current);
    }

    [Fact]
    public async Task Lokaler_Yolo_Export_nutzt_die_uebergebene_Klassenkarte()
    {
        var classMap = new RecordingClassMap();
        var request = TrainingYoloLocalExportRequestFactory.CreateWithDefaults(
            approvedSamples: [],
            outputDir: "output",
            evalImageHashes: new HashSet<string>(),
            evalHaltungKeys: new HashSet<string>(),
            persistSamplesAsync: () => Task.CompletedTask,
            log: _ => { },
            setProgressMax: _ => { },
            setProgressValue: _ => { },
            setStatusText: _ => { },
            cancellationToken: CancellationToken.None,
            yoloClasses: classMap);

        Assert.Equal(91, request.GetClassId("BAB"));
        Assert.Equal(91, request.GetFullClassMap()["BAB"]);
        await request.ExportClassesTxtAsync("classes.txt");

        Assert.Equal(1, classMap.GetClassIdCalls);
        Assert.Equal(1, classMap.GetFullMapCalls);
        Assert.Equal("classes.txt", classMap.LastExportPath);
    }

    [Fact]
    public void Player_und_Trainingscenter_reichen_die_registrierte_Klassenkarte_weiter()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var player = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"));
        var trainingWindow = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));
        var yoloExport = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.YoloExport.cs"));

        Assert.Contains("VsaYoloClasses: _protocolContext.VsaYoloClasses", player);
        Assert.Contains("vsaYoloClasses: services?.VsaYoloClasses", trainingWindow);
        Assert.Contains("_vsaYoloClasses", yoloExport);
    }

    private sealed class RecordingClassMap : IVsaYoloClassMapStore
    {
        public int GetClassIdCalls { get; private set; }
        public int GetFullMapCalls { get; private set; }
        public string? LastExportPath { get; private set; }

        public int GetClassId(string vsaCode)
        {
            GetClassIdCalls++;
            return 91;
        }

        public Dictionary<string, int> GetFullMap()
        {
            GetFullMapCalls++;
            return new Dictionary<string, int> { ["BAB"] = 91 };
        }

        public Task ExportClassesTxtAsync(string outputPath)
        {
            LastExportPath = outputPath;
            return Task.CompletedTask;
        }
    }
}
