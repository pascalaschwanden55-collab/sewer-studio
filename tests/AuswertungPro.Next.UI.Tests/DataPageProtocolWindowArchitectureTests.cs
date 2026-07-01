using System;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolWindowArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_protocol_window_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private void OpenProtocol(HaltungRecord? record)");

        Assert.Contains("_protocolWindowController.Open(record);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuswertungPro.Next.UI.Views.ProtocolObservationsWindow", method, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetDirectoryName", method, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveExistingPath(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SyncObservationsToHoldingFields(record)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSelectedProtocolEntries()", method, StringComparison.Ordinal);
    }

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method marker not found: {marker}");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method has no body: {marker}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method body is incomplete: {marker}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
