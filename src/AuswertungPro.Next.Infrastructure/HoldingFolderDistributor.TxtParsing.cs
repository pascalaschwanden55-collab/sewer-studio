using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Sprint 1 (2026-05-07): TXT-Parsing extrahiert aus
/// HoldingFolderDistributor.cs (4691 LOC). Bewegt die KINS-TXT-Section-Logik
/// in eine eigene Partial — gleiche Klasse, gleicher Namespace, identisches
/// Verhalten.
///
/// Methoden:
/// - ParseTxtSections: zerlegt eine kiDVDaten.txt in Haltungs-Sections
/// - TryParseTxtHeader: erkennt eine Header-Zeile mit Haltung + Video
/// - TryReadTxtDate: liefert das Inspektions-Datum aus kiDVinfo.txt
/// - TryParseDateFromInfoFile: parst Datums-Zeilen aus kiDVinfo.txt
/// - ReadAllTextLinesBestEffort: liest Datei Windows-1252 mit Fallback
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static IReadOnlyList<KinsTxtSection> ParseTxtSections(string txtPath)
    {
        var lines = ReadAllTextLinesBestEffort(txtPath);
        var sections = new List<KinsTxtSection>();
        var defaultDate = TryReadTxtDate(txtPath) ?? File.GetLastWriteTime(txtPath);

        string? currentHolding = null;
        string? currentVideo = null;
        var currentLines = new List<string>();

        void FlushCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentHolding))
                return;

            var content = string.Join(Environment.NewLine, currentLines);
            sections.Add(new KinsTxtSection(
                SourceTxtPath: txtPath,
                HoldingRaw: currentHolding,
                VideoFileName: currentVideo ?? string.Empty,
                Date: defaultDate,
                SectionText: content));

            currentHolding = null;
            currentVideo = null;
            currentLines = new List<string>();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            if (TryParseTxtHeader(line, out var haltung, out var videoFile))
            {
                FlushCurrent();
                currentHolding = haltung;
                currentVideo = videoFile;
                currentLines.Add(line.TrimEnd());
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentHolding))
                currentLines.Add(line.TrimEnd());
        }

        FlushCurrent();
        return sections;
    }

    private static bool TryParseTxtHeader(string line, out string haltung, out string videoFile)
    {
        haltung = string.Empty;
        videoFile = string.Empty;
        var match = KinsTxtHeaderRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var from = match.Groups["from"].Value.Trim();
        var to = match.Groups["to"].Value.Trim();
        var video = match.Groups["video"].Value.Trim();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;

        haltung = $"{from}-{to}";
        videoFile = video;
        return true;
    }

    private static DateTime? TryReadTxtDate(string txtPath)
    {
        var dir = Path.GetDirectoryName(txtPath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        var current = new DirectoryInfo(dir);
        for (var depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            var infoPath = Path.Combine(current.FullName, "kiDVinfo.txt");
            if (!File.Exists(infoPath))
                continue;

            var parsed = TryParseDateFromInfoFile(infoPath);
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    private static DateTime? TryParseDateFromInfoFile(string infoPath)
    {
        foreach (var line in ReadAllTextLinesBestEffort(infoPath))
        {
            var match = KinsTxtDateRegex.Match(line ?? string.Empty);
            if (!match.Success)
                continue;

            var raw = match.Groups["d"].Value;
            if (DateTime.TryParseExact(raw, new[] { "dd.MM.yy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadAllTextLinesBestEffort(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return File.ReadAllLines(path, Encoding.GetEncoding(1252));
        }
        catch
        {
            return File.ReadAllLines(path);
        }
    }
}
