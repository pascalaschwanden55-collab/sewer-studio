using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SystemMonitorProcessSafetyTests
{
    [Fact]
    public void SystemMonitorUsesSharedTimeoutProcessRunnerForExternalCommands()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
        Assert.DoesNotContain(".ReadToEnd()", source);
        Assert.DoesNotContain("WaitForExit(", source);
    }

    [Fact]
    public void MainWindowDisposesShellViewModelSoMonitorPollingStopsOnShutdown()
    {
        var windowSource = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml.cs"));
        var shellSource = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

        Assert.Contains("DataContext is IDisposable disposable", windowSource);
        Assert.Contains("disposable.Dispose();", windowSource);
        Assert.Contains("ShellViewModel : ObservableObject, IDisposable", shellSource);
        Assert.Contains("AiActivityTracker.ActiveChanged -= OnAiActivityChanged;", shellSource);
        Assert.Contains("AiRuntimeStatusTracker.Changed -= ApplyAiRuntimeStatus;", shellSource);
        Assert.Contains("Monitor.Dispose();", shellSource);
    }

    [Fact]
    public void SystemMonitorExposesHonestCpuTemperatureStatusAndSource()
    {
        var source = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"));

        Assert.Contains("public string CpuTempStatusText", source);
        Assert.Contains("public string CpuTempSourceLabel", source);
        Assert.Contains("SetCpuTempReading(cpuTempC, \"LibreHardwareMonitor\")", source);
        Assert.Contains("SetCpuTempReading(tempC, \"HWiNFO Shared Memory\")", source);
        Assert.Contains("SetCpuTempReading(celsius, \"Windows Thermal Zone\")", source);
        Assert.Contains("SetCpuTempUnavailable", source);
    }

    [Fact]
    public void PerformanceMonitorUsesCompactModernPanelAndAvoidsRemovedSensorStatusCard()
    {
        var mainWindowXaml = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));
        var panelXaml = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.UI", "Controls", "SystemMonitorPanel.xaml"));

        Assert.Contains("<ctrl:SystemMonitorPanel", mainWindowXaml);
        Assert.Contains("x:Name=\"PerformanceMonitorExpander\"", panelXaml);
        Assert.Contains("Text=\"Systemleistung\"", panelXaml);
        Assert.Contains("Text=\"Live-Monitor\"", panelXaml);
        Assert.Contains("Text=\"CPU Auslastung\"", panelXaml);
        Assert.Contains("Text=\"RAM Arbeitsspeicher\"", panelXaml);
        Assert.Contains("Text=\"GPU Grafikprozessor\"", panelXaml);
        Assert.Contains("Text=\"VRAM Videospeicher\"", panelXaml);
        Assert.DoesNotContain("Text=\"Sensorstatus\"", panelXaml);
        Assert.DoesNotContain("CpuTempStatusText", panelXaml);
        Assert.DoesNotContain("Text=\"LEISTUNGSMONITOR\"", mainWindowXaml);
        Assert.DoesNotContain("Text=\"LEISTUNGSMONITOR\"", panelXaml);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(SourceFilePath())! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
