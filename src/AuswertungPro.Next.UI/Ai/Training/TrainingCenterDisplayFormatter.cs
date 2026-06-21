using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingCenterDisplayFormatter
{
    public static string FormatRootFolders(IReadOnlyList<string> rootFolders)
        => rootFolders.Count switch
        {
            0 => "",
            1 => rootFolders[0],
            _ => $"{rootFolders.Count} Ordner: {string.Join("; ", rootFolders.Select(GetFolderDisplayName))}"
        };

    public static string FormatScanSummary(int total, int withProtocol, int pdfOnly)
    {
        var withoutProtocol = Math.Max(0, total - withProtocol);
        var parts = new List<string> { $"Gefunden: {total} Fälle" };
        if (pdfOnly > 0)
            parts.Add($"{pdfOnly} nur PDF");
        if (withoutProtocol > 0)
            parts.Add($"{withoutProtocol} ohne Protokoll");

        return string.Join(", ", parts);
    }

    private static string GetFolderDisplayName(string folder)
    {
        var trimmed = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(name) ? folder : name;
    }
}
