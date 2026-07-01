using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageAutoSaveArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_autosave_entscheidungen_an_controller()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));
        var timerController = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageTimerController.cs"));

        var scheduleBody = ExtractMethodBody(source, "public void ScheduleAutoSave()");
        var tickBody = ExtractMethodBody(source, "private void AutoSaveOnTimerTick()");

        Assert.Contains("_timers.ScheduleAutoSave(", scheduleBody, StringComparison.Ordinal);
        Assert.Contains("_timers.HandleAutoSaveTimerTick(", tickBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", source, StringComparison.Ordinal);
        Assert.Contains("DataPageAutoSaveController.Schedule(", timerController, StringComparison.Ordinal);
        Assert.Contains("DataPageAutoSaveController.HandleTimerTick(", timerController, StringComparison.Ordinal);
        Assert.DoesNotContain("switch (mode)", scheduleBody, StringComparison.Ordinal);
        Assert.DoesNotContain("mode is not (AutoSaveMode.Every5Minutes or AutoSaveMode.Every10Minutes)", tickBody, StringComparison.Ordinal);
    }

    [Fact]
    public void DataPageViewModel_delegiert_save_status_banner_an_controller()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));
        var timerController = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "DataPage", "DataPageTimerController.cs"));

        var showBody = ExtractMethodBody(source, "private void ShowSaveStatus(string? text)");

        Assert.Contains("_timers.ShowSaveStatus(", showBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatcherTimer", source, StringComparison.Ordinal);
        Assert.Contains("DataPageSaveStatusController.Hide(", timerController, StringComparison.Ordinal);
        Assert.Contains("DataPageSaveStatusController.Show(", timerController, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveStatus = string.IsNullOrWhiteSpace(text)", showBody, StringComparison.Ordinal);
        Assert.DoesNotContain("_saveBannerTimer.Stop();", showBody, StringComparison.Ordinal);
        Assert.DoesNotContain("_saveBannerTimer.Start();", showBody, StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Repository-Root mit AuswertungPro.sln wurde nicht gefunden.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var braceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceIndex..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenrumpf nicht abgeschlossen: {signature}");
    }
}
