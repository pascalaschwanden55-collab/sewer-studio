using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCommandTargetControllerTests
{
    [Fact]
    public void HasTarget_ist_true_wenn_command_record_oder_selected_existiert()
    {
        var commandRecord = new HaltungRecord();
        var selected = new HaltungRecord();

        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord, selected: null));
        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord: null, selected));
        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord, selected));
    }

    [Fact]
    public void HasTarget_ist_false_ohne_command_record_und_ohne_selected()
        => Assert.False(DataPageCommandTargetController.HasTarget(commandRecord: null, selected: null));

    [Fact]
    public void DataPageViewModel_delegiert_command_target_pruefungen()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));

        AssertDelegates(source, "private bool CanOpenCosts(HaltungRecord? record)");
        AssertDelegates(source, "private bool CanRestoreCosts(HaltungRecord? record)");
        AssertDelegates(source, "private bool CanSuggestMeasures(HaltungRecord? record)");
    }

    private static void AssertDelegates(string source, string signature)
    {
        var body = ExtractMethodBody(source, signature);

        Assert.Contains("DataPageCommandTargetController.HasTarget(record, Selected)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("if (record is not null)", body, StringComparison.Ordinal);
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Signatur nicht gefunden: {signature}");

        var arrowIndex = source.IndexOf("=>", signatureIndex, StringComparison.Ordinal);
        var nextBraceIndex = source.IndexOf('{', signatureIndex);
        if (arrowIndex >= 0 && (nextBraceIndex < 0 || arrowIndex < nextBraceIndex))
        {
            var semicolonIndex = source.IndexOf(';', arrowIndex);
            Assert.True(semicolonIndex >= 0, $"Expression-Body nicht abgeschlossen: {signature}");
            return source[signatureIndex..(semicolonIndex + 1)];
        }

        var braceIndex = nextBraceIndex;
        Assert.True(braceIndex >= 0, $"Methodenrumpf nicht gefunden: {signature}");
        var depth = 0;
        for (var i = braceIndex; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
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
