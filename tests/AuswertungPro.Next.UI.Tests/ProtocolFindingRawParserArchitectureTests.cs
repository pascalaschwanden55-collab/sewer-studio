using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolFindingRawParserArchitectureTests
{
    [Fact]
    public void Protokollfenster_und_Pdf_Aufbereitung_nutzen_denselben_Rohtextparser()
    {
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "ProtocolObservationsWindow.xaml.cs"));
        var pdfText = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.Application",
            "Reports",
            "ProtocolPdfObservationText.cs"));
        var mapper = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "VsaFindingToProtocolEntryMapper.cs"));
        var mediaLinkController = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "DataPageProtocolMediaLinkController.cs"));
        var observationsWindow = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "BeobachtungenWindow.xaml.cs"));
        var xtfNormalizer = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.Infrastructure",
            "Import",
            "Xtf",
            "XtfValueNormalizer.cs"));
        var inputNormalizer = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.Application",
            "Protocol",
            "ProtocolEntryInputNormalizer.cs"));
        var catalogViewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Protocol",
            "ObservationCatalogViewModel.cs"));

        Assert.Equal(2, CountOccurrences(window, "ProtocolTimeParser.ParseMpegTime("));
        Assert.Equal(1, CountOccurrences(window, "ProtocolFindingRawParser.TryParseMeterFromRaw("));
        Assert.Equal(1, CountOccurrences(window, "ProtocolFindingRawParser.TryParseSecondMeterFromRaw("));
        Assert.Equal(1, CountOccurrences(window, "ProtocolFindingRawParser.TryParseTimeFromRaw("));
        var importMethodStart = window.IndexOf(
            "private IReadOnlyList<ProtocolEntry> BuildImportedEntries",
            StringComparison.Ordinal);
        Assert.True(importMethodStart >= 0, "BuildImportedEntries wurde nicht gefunden.");
        var importMethod = window[importMethodStart..];
        AssertInOrder(
            importMethod,
            "mStart = ProtocolFindingRawParser.TryParseMeterFromRaw(f.Raw);",
            "mEnd = ProtocolFindingRawParser.TryParseSecondMeterFromRaw(f.Raw);",
            "var time = ProtocolTimeParser.ParseMpegTime(f.MPEG)",
            "?? (f.Timestamp is null ? null : f.Timestamp.Value.TimeOfDay);",
            "var rawTime = ProtocolFindingRawParser.TryParseTimeFromRaw(f.Raw);",
            "time = ProtocolTimeParser.ParseMpegTime(rawTime);",
            "f.MPEG = rawTime;");

        Assert.DoesNotContain("private static TimeSpan? ParseMpegTime", window);
        Assert.DoesNotContain("private static double? TryParseMeterFromRaw", window);
        Assert.DoesNotContain("private static double? TryParseSecondMeterFromRaw", window);
        Assert.DoesNotContain("private static string? TryParseTimeFromRaw", window);
        Assert.DoesNotContain("RawMeterRegex", window);
        Assert.DoesNotContain("RawTimeRegex", window);

        AssertDelegates(
            pdfText,
            "internal static TimeSpan? ParseMpegTime",
            "internal static double? TryParseDouble",
            "ProtocolTimeParser.ParseMpegTime(raw)");
        AssertDelegates(
            pdfText,
            "internal static double? TryParseMeterFromRaw",
            "internal static double? TryParseSecondMeterFromRaw",
            "ProtocolFindingRawParser.TryParseMeterFromRaw(raw)");
        AssertDelegates(
            pdfText,
            "internal static double? TryParseSecondMeterFromRaw",
            "internal static string? TryParseTimeFromRaw",
            "ProtocolFindingRawParser.TryParseSecondMeterFromRaw(raw)");
        AssertDelegates(
            pdfText,
            "internal static string? TryParseTimeFromRaw",
            "internal static string? GetParam",
            "ProtocolFindingRawParser.TryParseTimeFromRaw(raw)");
        Assert.DoesNotContain("RawMeterRegex", pdfText);
        Assert.DoesNotContain("RawTimeRegex", pdfText);

        Assert.Contains("ProtocolTimeParser.ParseMpegTime(f.MPEG)", mapper);
        Assert.Contains("ProtocolTimeParser.ParseMpegTime(entry.Mpeg)", mediaLinkController);
        Assert.Contains("ProtocolTimeParser.ParseMpegTime(entry.Mpeg)", observationsWindow);
        Assert.DoesNotContain("private static TimeSpan? ParseMpegTime", mapper);
        Assert.DoesNotContain("private static TimeSpan? ParseMpegTime", mediaLinkController);
        Assert.DoesNotContain("private static TimeSpan? ParseMpegTime", observationsWindow);
        AssertDelegates(
            xtfNormalizer,
            "public static TimeSpan? ParseMpegTime",
            "}",
            "ProtocolTimeParser.ParseMpegTime(raw)");
        AssertDelegates(
            inputNormalizer,
            "public static bool TryParseOptionalTimeSpan",
            "public static TimeSpan? TryParseTimeFallback",
            "ProtocolTimeParser.ParseMpegTime(raw)");
        Assert.DoesNotContain("TimeSpan.TryParse", inputNormalizer);
        Assert.Contains(
            "ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(ZeitText, out var zeit)",
            catalogViewModel);
        Assert.DoesNotContain("private static bool TryParseOptionalTimeSpan", catalogViewModel);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void AssertDelegates(
        string source,
        string startMarker,
        string endMarker,
        string expectedCall)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{startMarker}' wurde nicht gefunden.");
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"'{endMarker}' wurde nicht nach '{startMarker}' gefunden.");

        Assert.Contains(expectedCall, source[start..end]);
    }

    private static void AssertInOrder(string source, params string[] expectedParts)
    {
        var previousIndex = -1;
        foreach (var part in expectedParts)
        {
            var currentIndex = source.IndexOf(part, previousIndex + 1, StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex, $"'{part}' steht nicht an der erwarteten Stelle.");
            previousIndex = currentIndex;
        }
    }
}
