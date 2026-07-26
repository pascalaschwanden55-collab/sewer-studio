using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Strenger, gemeinsam verwendeter Vertrag fuer den KI-Spiegel-Marker.</summary>
internal static class KnowledgeMirrorMarker
{
    public const string Header = "SewerStudio KI_BRAIN Echtzeit-Spiegel";

    public static void Validate(string content, string expectedSourceRoot, string expectedTargetRoot)
    {
        ArgumentNullException.ThrowIfNull(content);
        var lines = ReadLines(content);
        if (lines.Count == 0 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
            throw new InvalidDataException("Der KI-Spiegel-Marker besitzt keinen gueltigen Kopf.");

        var source = ReadSingleValue(lines, "Source");
        if (!PathsEqual(source, expectedSourceRoot))
            throw new InvalidDataException("Der KI-Spiegel-Marker gehoert zu einer anderen Quelle.");

        var target = ReadSingleValue(lines, "Target");
        if (!PathsEqual(target, expectedTargetRoot))
            throw new InvalidDataException("Der KI-Spiegel-Marker gehoert zu einem anderen Ziel.");
    }

    public static bool MatchesLegacyLog(
        string content,
        string expectedSourceRoot,
        string expectedTargetRoot)
    {
        try
        {
            var lines = ReadLines(content);
            var source = ReadSingleLegacyValue(lines, "Quelle");
            var target = ReadSingleLegacyValue(lines, "Ziel");
            return PathsEqual(source, expectedSourceRoot)
                   && PathsEqual(target, expectedTargetRoot);
        }
        catch (Exception ex) when (ex is InvalidDataException
                                   or ArgumentException
                                   or NotSupportedException
                                   or PathTooLongException)
        {
            return false;
        }
    }

    private static string ReadSingleValue(IReadOnlyList<string> lines, string key)
    {
        string? value = null;
        var matches = 0;
        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (separator <= 0
                || !string.Equals(line[..separator], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            value = line[(separator + 1)..];
        }

        if (matches != 1 || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Der KI-Spiegel-Marker besitzt keine eindeutige {key}-Zeile.");
        }

        return value;
    }

    private static string ReadSingleLegacyValue(IReadOnlyList<string> lines, string key)
    {
        string? value = null;
        var matches = 0;
        foreach (var line in lines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0
                || !string.Equals(
                    line[..separator].Trim(),
                    key,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            value = line[(separator + 1)..].Trim();
        }

        if (matches != 1 || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Der alte KI-Spiegel-Log besitzt keine eindeutige {key}-Zeile.");
        }

        return value;
    }

    private static List<string> ReadLines(string content)
    {
        var lines = new List<string>();
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
