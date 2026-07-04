using System.IO;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SystemMonitorProcessSafetyTests
{
    [Fact]
    public void SystemMonitor_can_skip_native_hardware_sensor_initialization()
    {
        using var monitor = new SystemMonitorService(enableHardwareSensorInit: false);

        monitor.Start();
        monitor.Stop();

        Assert.False(monitor.IsSensorBlocked);
    }

    [Fact]
    public void SystemMonitorUsesSharedTimeoutProcessRunnerForExternalCommands()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
    }

    [Fact]
    public void MainWindowDisposesDataContextOnShutdown()
    {
        var windowSource = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml.cs"));

        Assert.Contains("DataContext is IDisposable disposable", windowSource);
        Assert.Contains("disposable.Dispose();", windowSource);
    }

    [Fact]
    public void ShellViewModelDisposeDetachesGlobalAiStatusSubscriptions()
    {
        AiRuntimeStatusTracker.ResetForTests();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var monitor = new SystemMonitorService(enableHardwareSensorInit: false);
        using var shell = new ShellViewModel(services, monitor);

        using (AiActivityTracker.Begin("Analyse vor Dispose"))
        {
            Assert.True(shell.IsAiWorking);
            Assert.Equal("Analyse vor Dispose", shell.AiStatusLabel);
        }

        AiRuntimeStatusTracker.MarkStarting("Modell vor Dispose");
        Assert.Equal("KI STARTET", shell.AiRuntimeTitle);

        shell.Dispose();

        using (AiActivityTracker.Begin("Analyse nach Dispose"))
        {
            Assert.False(shell.IsAiWorking);
            Assert.NotEqual("Analyse nach Dispose", shell.AiStatusLabel);
        }

        AiRuntimeStatusTracker.MarkReady("Modell nach Dispose", hasWarnings: false);
        Assert.Equal("KI STARTET", shell.AiRuntimeTitle);
    }

    [Fact]
    public void SystemMonitorExposesHonestCpuTemperatureStatusAndSource()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Services", "SystemMonitorService.cs"));

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
        var mainWindowXaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));
        var panelXaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Controls", "SystemMonitorPanel.xaml"));

        Assert.Contains("<ctrl:SystemMonitorPanel", mainWindowXaml);
        Assert.Contains("x:Name=\"PerformanceMonitorExpander\"", panelXaml);
        Assert.Contains("Text=\"Systemleistung\"", panelXaml);
        Assert.Contains("Text=\"Live-Monitor\"", panelXaml);
        Assert.Contains("Text=\"CPU Auslastung\"", panelXaml);
        Assert.Contains("Text=\"RAM Arbeitsspeicher\"", panelXaml);
        Assert.Contains("Text=\"GPU Grafikprozessor\"", panelXaml);
        Assert.Contains("Text=\"VRAM Videospeicher\"", panelXaml);
    }

}
