using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer FindingEntryMatcher.
/// Deckt das IST-Verhalten des Distanz-/Code-/Foto-Scorers ab,
/// der aus LegacyXtfImportService extrahiert wurde.
/// </summary>
public sealed class FindingEntryMatcherTests
{
    // ===================== GetFindingMeterStart / GetFindingMeterEnd =====================

    [Fact]
    public void GetFindingMeterStart_PrefersExplicitMeterStart()
    {
        var finding = new VsaFinding { MeterStart = 5.0, SchadenlageAnfang = 10.0 };
        Assert.Equal(5.0, FindingEntryMatcher.GetFindingMeterStart(finding));
    }

    [Fact]
    public void GetFindingMeterStart_FallsBackToSchadenlageAnfang_WhenMeterStartNull()
    {
        var finding = new VsaFinding { MeterStart = null, SchadenlageAnfang = 10.0 };
        Assert.Equal(10.0, FindingEntryMatcher.GetFindingMeterStart(finding));
    }

    [Fact]
    public void GetFindingMeterStart_ReturnsNull_WhenBothNull()
    {
        var finding = new VsaFinding { MeterStart = null, SchadenlageAnfang = null };
        Assert.Null(FindingEntryMatcher.GetFindingMeterStart(finding));
    }

    [Fact]
    public void GetFindingMeterEnd_PrefersExplicitMeterEnd()
    {
        var finding = new VsaFinding { MeterEnd = 8.0, SchadenlageEnde = 12.0 };
        Assert.Equal(8.0, FindingEntryMatcher.GetFindingMeterEnd(finding));
    }

    [Fact]
    public void GetFindingMeterEnd_FallsBackToSchadenlageEnde_WhenMeterEndNull()
    {
        var finding = new VsaFinding { MeterEnd = null, SchadenlageEnde = 12.0 };
        Assert.Equal(12.0, FindingEntryMatcher.GetFindingMeterEnd(finding));
    }

    // ===================== FindBestFindingForEntry =====================

    [Fact]
    public void FindBestFindingForEntry_ReturnsNull_WhenEmptyList()
    {
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 5.0 };
        var result = FindingEntryMatcher.FindBestFindingForEntry(entry, new List<VsaFinding>());
        Assert.Null(result);
    }

    [Fact]
    public void FindBestFindingForEntry_SelectsExactMeterMatch_WithinTolerance()
    {
        // Zwei Befunde: einer sehr nah (0.05m), einer weit entfernt (2m)
        var near = new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.05 };
        var far = new VsaFinding { KanalSchadencode = "BAB", MeterStart = 7.0 };
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 5.0 };

        var result = FindingEntryMatcher.FindBestFindingForEntry(entry, new List<VsaFinding> { far, near });
        Assert.Same(near, result);
    }

    [Fact]
    public void FindBestFindingForEntry_PrefersClosestMeter_InEngeToleranz()
    {
        // Enge Toleranz: beide Befunde sind <= 0.15m entfernt → der naehergelegene gewinnt
        var nearer = new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.05 };
        var farther = new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.12 };
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 5.0 };

        var result = FindingEntryMatcher.FindBestFindingForEntry(
            entry, new List<VsaFinding> { farther, nearer });
        Assert.Same(nearer, result);
    }

    [Fact]
    public void FindBestFindingForEntry_LockerToleranz_ExcludesCodeRank2()
    {
        // Lockere Toleranz (0.15 < Delta <= 0.50): Code-Rang muss <= 1 sein
        // Befund A: Delta=0.30, CodeRank=0 (exakter Code) → Treffer
        // Befund B: Delta=0.10 → faellt NICHT rein, da enge Toleranz bereits leer ist
        //           (Delta=0.10 waere enge Toleranz <= 0.15 – hier beide ausserhalb)
        // Korrektere Variante: eine lockere Situation konstruieren
        var codeMatch = new VsaFinding { KanalSchadencode = "BAB", MeterStart = 5.3 }; // Delta=0.30, CodeRank=0
        var noCodeMatch = new VsaFinding { KanalSchadencode = "BBC", MeterStart = 5.4 }; // Delta=0.40, CodeRank=2
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 5.0 };

        // locker: Delta <= 0.50 und CodeRank <= 1 → codeMatch qualifiziert, noCodeMatch nicht
        var result = FindingEntryMatcher.FindBestFindingForEntry(
            entry, new List<VsaFinding> { noCodeMatch, codeMatch });
        Assert.Same(codeMatch, result);
    }

    [Fact]
    public void FindBestFindingForEntry_FallsBackToCodeOnly_WhenNoMeterAvailable()
    {
        // Kein MeterStart im Eintrag → Code-Rang entscheidet
        var exact = new VsaFinding { KanalSchadencode = "BAB" };
        var prefix = new VsaFinding { KanalSchadencode = "BABA" };
        var noMatch = new VsaFinding { KanalSchadencode = "BBC" };
        var entry = new ProtocolEntry { Code = "BAB" };

        var result = FindingEntryMatcher.FindBestFindingForEntry(
            entry, new List<VsaFinding> { noMatch, prefix, exact });
        Assert.Same(exact, result);
    }

    [Fact]
    public void FindBestFindingForEntry_PrefersPhoto_AsTiebreaker()
    {
        // Zwei Befunde, beide Code-Rang 0 (exakt), kein Meter → Foto bricht Gleichstand
        var withPhoto = new VsaFinding { KanalSchadencode = "BCD", FotoPath = "img.jpg" };
        var noPhoto = new VsaFinding { KanalSchadencode = "BCD", FotoPath = null };
        var entry = new ProtocolEntry { Code = "BCD" };

        var result = FindingEntryMatcher.FindBestFindingForEntry(
            entry, new List<VsaFinding> { noPhoto, withPhoto });
        Assert.Same(withPhoto, result);
    }

    [Fact]
    public void FindBestFindingForEntry_ReturnsSomething_WhenNoCodeAndNoMeter()
    {
        // Kein Meter, kein Code-Match → Fallback gibt irgendwas zurueck (nicht null)
        var finding = new VsaFinding { KanalSchadencode = "BBA", MeterStart = 3.0 };
        var entry = new ProtocolEntry { Code = "XYZ" };

        var result = FindingEntryMatcher.FindBestFindingForEntry(
            entry, new List<VsaFinding> { finding });
        Assert.NotNull(result);
    }
}
