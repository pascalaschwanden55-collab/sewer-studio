using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiStartupUiTests
{
    [Fact]
    public void MainWindow_tools_menu_contains_ai_start_entry()
    {
        var xaml = ReadUiFile("MainWindow.xaml");
        var fileMenu = ExtractSection(xaml, "<MenuItem Header=\"_Datei\"", "<MenuItem Header=\"_Werkzeuge\"");
        var toolsMenu = ExtractSection(xaml, "<MenuItem Header=\"_Werkzeuge\"", "<MenuItem Header=\"_Ansicht\"");

        AssertNoForbiddenTokens(fileMenu, "Header=\"KI starten\"");
        Assert.Contains("Header=\"KI starten\"", toolsMenu);
        Assert.Contains("Click=\"StartAi_Click\"", toolsMenu);
    }

    [Fact]
    public void SettingsPage_contains_ai_start_controls()
    {
        var xaml = ReadUiFile("Views", "Pages", "SettingsPage.xaml");

        Assert.Contains("KI beim Programmstart starten", xaml);
        Assert.Contains("StartAiOnProgramStart", xaml);
        Assert.Contains("StartAiCommand", xaml);
        Assert.Contains("AiStartupStatusText", xaml);
    }

    [Fact]
    public void Shell_tracks_ai_runtime_status_without_sidebar_neural_sphere()
    {
        var xaml = ReadUiFile("MainWindow.xaml");
        AiRuntimeStatusTracker.ResetForTests();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings(),
            new DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);
        using var monitor = new SystemMonitorService(enableHardwareSensorInit: false);
        using var shell = new ShellViewModel(services, monitor);

        AiRuntimeStatusTracker.MarkReady("qwen3-vl:8b-q8", hasWarnings: false, statusText: "Modelle geladen");

        Assert.True(shell.IsAiIndicatorVisible);
        Assert.Equal("KI BEREIT", shell.AiIndicatorTitle);
        Assert.Equal("Modelle geladen", shell.AiDisplayStatusLabel);
        Assert.Equal("qwen3-vl:8b-q8", shell.AiDisplayLoadedModels);
        AssertNoForbiddenTokens(xaml, "<!-- KI Neural Sphere -->", "<ctrl:NeuralSphereControl");
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");

        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"End marker not found: {endMarker}");

        return source.Substring(start, end - start);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte KI-UI-Markierung gefunden: " + string.Join(", ", hits));
    }
}
