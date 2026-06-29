using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterBatchImportArchitectureTests
{
    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_run_control_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var cancelSource = ExtractMethodBody(source, "private void CancelBatch()");

        Assert.Contains("TrainingBatchImportRunControlController.RequestCancel(_genCts)", cancelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts?.Cancel();", cancelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TrainingBatchImportTerminalPresentationBuilder.BuildCancelRequestedStatus", cancelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void TrainingCenterViewModel_delegiert_batch_import_run_preparation_an_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportRunPreparationController.Prepare(", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("_rootFolders.Count", batchImportSource, StringComparison.Ordinal);
        Assert.Contains("_genCts = runPreparation.CancellationTokenSource;", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts?.Cancel();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts?.Dispose();", batchImportSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_genCts = new CancellationTokenSource();", batchImportSource, StringComparison.Ordinal);
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
