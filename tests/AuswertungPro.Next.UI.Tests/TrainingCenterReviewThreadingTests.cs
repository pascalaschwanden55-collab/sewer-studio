using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterReviewThreadingTests
{
    [Fact]
    public void ReviewFreigabe_LaedtSamplesNurUeberUiDispatcher()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        var method = ExtractMethod(source, "private async Task LoadSamplesInternalAsync()");

        Assert.Contains("OnUi(() =>", method);
        Assert.Contains("ObservableCollectionContentController.ReplaceWith(Samples, list)", method);
        Assert.True(
            method.IndexOf("OnUi(() =>", StringComparison.Ordinal)
            < method.IndexOf("ObservableCollectionContentController.ReplaceWith(Samples, list)", StringComparison.Ordinal),
            "Samples-Replace muss ueber den UI-Dispatcher laufen; Review-Freigaben koennen nach ConfigureAwait(false) auf einem Hintergrundthread fortsetzen.");
    }

    [Fact]
    public void StartdatenSammelfreigabe_NutztDispatcherSnapshotDerReviewQueue()
    {
        var source = File.ReadAllText(FindRepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs"));

        var method = ExtractMethod(source, "public async Task ApproveAllStartdataAsync(CancellationToken ct = default)");

        Assert.Contains("GetProtocolStartdataReviewItems()", method);
        Assert.DoesNotContain("ReviewQueue\r\n            .Where", method);
        Assert.DoesNotContain("ReviewQueue\n            .Where", method);
    }

    private static string ExtractMethod(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Methode nicht gefunden: {signature}");

        var brace = source.IndexOf('{', start);
        Assert.True(brace > start, $"Methodenrumpf nicht gefunden: {signature}");

        var depth = 0;
        for (var i = brace; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[start..(i + 1)];
            }
        }

        throw new InvalidOperationException($"Methodenende nicht gefunden: {signature}");
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
