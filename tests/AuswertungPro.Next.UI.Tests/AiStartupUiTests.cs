using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiStartupUiTests
{
    [Fact]
    public void MainWindow_tools_menu_contains_ai_start_entry()
    {
        var xaml = ReadUiFile("MainWindow.xaml");
        var fileMenu = ExtractSection(xaml, "<MenuItem Header=\"_Datei\"", "<MenuItem Header=\"_Werkzeuge\"");
        var toolsMenu = ExtractSection(xaml, "<MenuItem Header=\"_Werkzeuge\"", "<MenuItem Header=\"_Ansicht\"");

        Assert.DoesNotContain("Header=\"KI starten\"", fileMenu);
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
    public void MainWindow_ai_sidebar_shows_started_runtime_status_not_only_active_work()
    {
        var xaml = ReadUiFile("MainWindow.xaml");
        var sidebar = ExtractSection(xaml, "<!-- KI Neural Sphere -->", "<ListBox ItemsSource=");

        Assert.Contains("IsAiIndicatorVisible", sidebar);
        Assert.Contains("AiIndicatorTitle", sidebar);
        Assert.Contains("AiDisplayLoadedModels", sidebar);
        Assert.Contains("AiDisplayStatusLabel", sidebar);
        Assert.Contains("IsActive=\"{Binding IsAiWorking}\"", sidebar);
        Assert.DoesNotContain("Binding=\"{Binding IsAiWorking}\" Value=\"True\"", sidebar);
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

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}
