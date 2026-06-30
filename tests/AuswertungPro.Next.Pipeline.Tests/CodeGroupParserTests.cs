using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer CodeGroupParser (IST-Verhalten aus ProtocolCodePickerViewModel).
/// </summary>
public sealed class CodeGroupParserTests
{
    // ── ParseGroup: Kein Schraegstrich ────────────────────────────────────────

    [Fact]
    public void ParseGroup_null_ergibt_unbekannt_fuer_beide()
    {
        var (major, @base) = CodeGroupParser.ParseGroup(null);
        Assert.Equal("Unbekannt", major);
        Assert.Equal("Unbekannt", @base);
    }

    [Fact]
    public void ParseGroup_leerstring_ergibt_unbekannt_fuer_beide()
    {
        var (major, @base) = CodeGroupParser.ParseGroup(string.Empty);
        Assert.Equal("Unbekannt", major);
        Assert.Equal("Unbekannt", @base);
    }

    [Fact]
    public void ParseGroup_nur_leerzeichen_ergibt_unbekannt_fuer_beide()
    {
        var (major, @base) = CodeGroupParser.ParseGroup("   ");
        Assert.Equal("Unbekannt", major);
        Assert.Equal("Unbekannt", @base);
    }

    [Fact]
    public void ParseGroup_einfacher_name_ohne_slash_gibt_gleichen_wert_fuer_major_und_base()
    {
        var (major, @base) = CodeGroupParser.ParseGroup("Struktur");
        Assert.Equal("Struktur", major);
        Assert.Equal("Struktur", @base);
    }

    // ── ParseGroup: Mit Schraegstrich ─────────────────────────────────────────

    [Fact]
    public void ParseGroup_slash_trennt_major_und_base()
    {
        var (major, @base) = CodeGroupParser.ParseGroup("Strukturell/Risse");
        Assert.Equal("Strukturell", major);
        Assert.Equal("Risse", @base);
    }

    [Fact]
    public void ParseGroup_leerzeichen_um_slash_werden_getrimmt()
    {
        var (major, @base) = CodeGroupParser.ParseGroup("Strukturell / Risse");
        Assert.Equal("Strukturell", major);
        Assert.Equal("Risse", @base);
    }

    [Fact]
    public void ParseGroup_nur_ein_slash_erster_teil_ist_major_zweiter_ist_base()
    {
        var (major, @base) = CodeGroupParser.ParseGroup("A/B/C");
        // Split mit Limit 2: "A" und "B/C"
        Assert.Equal("A", major);
        Assert.Equal("B/C", @base);
    }

    // ── NormalizeGroup ────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeGroup_null_ergibt_unbekannt()
        => Assert.Equal("Unbekannt", CodeGroupParser.NormalizeGroup(null));

    [Fact]
    public void NormalizeGroup_leerstring_ergibt_unbekannt()
        => Assert.Equal("Unbekannt", CodeGroupParser.NormalizeGroup(string.Empty));

    [Fact]
    public void NormalizeGroup_wert_wird_getrimmt()
        => Assert.Equal("Betrieb", CodeGroupParser.NormalizeGroup("  Betrieb  "));

    [Fact]
    public void NormalizeGroup_wert_bleibt_unveraendert_wenn_schon_normiert()
        => Assert.Equal("Struktur", CodeGroupParser.NormalizeGroup("Struktur"));
}
