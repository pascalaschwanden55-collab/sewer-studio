using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
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
        Assert.Same(
            services.TrainingYoloClasses,
            services.GetService(typeof(ITrainingYoloClassMapStore)));
    }

    [Fact]
    public void ServiceProvider_laesst_class_map_v2_strikt_und_gebunden_einlesen()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        var snapshot = services.TrainingYoloClasses.ReadSnapshot();

        Assert.Equal(YoloDetectClassMapV2.Version, snapshot.Version);
        Assert.Equal(14, snapshot.Classes.Count);
        Assert.Equal(13, snapshot.Classes["SONST_schaden"]);
        Assert.Throws<TrainingYoloClassMapException>(() => snapshot.ResolveRequired("BAB"));
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
    public void Player_reicht_Teacher_Klassenkarte_weiter_und_Trainingscenter_nur_den_Exportkoordinator()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var player = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs"));
        var trainingWindow = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingCenterWindow.xaml.cs"));
        var yoloExport = File.ReadAllText(Path.Combine(
            root, "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.YoloExport.cs"));

        Assert.Contains("VsaYoloClasses: _protocolContext.VsaYoloClasses", player);
        Assert.Contains("trainingYoloExport: services?.TrainingYoloExport", trainingWindow);
        Assert.DoesNotContain("trainingYoloClasses:", trainingWindow);
        Assert.Contains("TrainingYoloExportRequestFactory.Create(", yoloExport);
        Assert.Contains("_trainingYoloExport", yoloExport);
        Assert.DoesNotContain("_trainingYoloClasses", yoloExport);
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

        public int GetOrAddClassId(string vsaCode)
            => 91;

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
