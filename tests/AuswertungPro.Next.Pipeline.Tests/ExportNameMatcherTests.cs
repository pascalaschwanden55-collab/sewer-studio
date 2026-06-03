using System.Collections.Generic;
using AuswertungPro.Next.Infrastructure.Media;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class ExportNameMatcherTests
{
    // Reale Dateinamen aus dem Export (Punkt -> Unterstrich, _0001 Sektionssuffix).
    private static readonly List<string> Files = new()
    {
        @"D:\V\Sec\175_1-408_0001.mp4",
        @"D:\V\Sec\1_0001.mp4",
        @"D:\V\Sec\2_0001.mp4",
        @"D:\V\Sec\7_420-408_0001.mp4",
        @"D:\V\Sec\7_420-408_0002.mp4",
    };

    [Fact]
    public void Punkt_zu_Unterstrich_mit_Sektion_wird_gefunden()
    {
        var (status, path, _) = ExportNameMatcher.Match(Files, "175.1-408");
        Assert.Equal(MediaMatchStatus.Found, status);
        Assert.EndsWith("175_1-408_0001.mp4", path);
    }

    [Fact]
    public void Mehrere_Abschnitte_derselben_Haltung_ergeben_eindeutigen_ersten_Treffer()
    {
        var (status, path, _) = ExportNameMatcher.Match(Files, "7.420-408");
        Assert.Equal(MediaMatchStatus.Found, status);
        Assert.EndsWith("7_420-408_0001.mp4", path); // erster Abschnitt
    }

    [Fact]
    public void Einstellige_Haltung_1_trifft_nur_1_0001()
    {
        var (status, path, _) = ExportNameMatcher.Match(Files, "1");
        Assert.Equal(MediaMatchStatus.Found, status);
        Assert.EndsWith("1_0001.mp4", path);
    }

    [Fact]
    public void Einstellige_Haltung_2_trifft_nur_2_0001()
    {
        var (status, path, _) = ExportNameMatcher.Match(Files, "2");
        Assert.Equal(MediaMatchStatus.Found, status);
        Assert.EndsWith("2_0001.mp4", path);
    }

    [Fact]
    public void Einstellige_Haltung_matcht_keine_laengere_Haltung()
    {
        // "1" darf NICHT "175_1-408_0001" treffen (kein Trennzeichen nach der fuehrenden 1).
        Assert.False(ExportNameMatcher.NameMatches("175_1-408_0001", "1"));
        Assert.True(ExportNameMatcher.NameMatches("1_0001", "1"));
    }

    [Fact]
    public void Nicht_passende_Haltung_ergibt_NotFound()
    {
        var (status, _, _) = ExportNameMatcher.Match(Files, "999.9-999");
        Assert.Equal(MediaMatchStatus.NotFound, status);
    }

    [Fact]
    public void Exakter_Name_ohne_Sektion_wird_gefunden()
    {
        var files = new List<string> { @"C:\x\175_1-408.mp4" };
        var (status, _, _) = ExportNameMatcher.Match(files, "175.1-408");
        Assert.Equal(MediaMatchStatus.Found, status);
    }

    [Fact]
    public void Rest_hinter_Haltung_darf_nicht_aus_Buchstaben_bestehen()
    {
        // "175_1-408_extra" ist KEINE reine Sektionsnummer -> kein Treffer.
        Assert.False(ExportNameMatcher.NameMatches("175_1-408_extra", "175.1-408"));
    }
}
