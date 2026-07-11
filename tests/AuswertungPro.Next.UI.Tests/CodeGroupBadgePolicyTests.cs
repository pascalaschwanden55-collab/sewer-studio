using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>VSA-Hauptgruppe -> Badge (BrushKey, Glyph, Kurzlabel) fuer Chips und Code-Badges.</summary>
public sealed class CodeGroupBadgePolicyTests
{
    [Fact]
    public void Struktur_gruppe_BA()
    {
        var badge = CodeGroupBadgePolicy.Resolve("BAB");
        Assert.Equal("CodeGroupStrukturBrush", badge.BrushKey);
        Assert.Equal("CodeGroupStrukturSubtleBrush", badge.SubtleBrushKey);
        Assert.Equal("Struktur", badge.Kurzlabel);
    }

    [Fact]
    public void Betrieb_gruppe_BB()
    {
        var badge = CodeGroupBadgePolicy.Resolve("BBC");
        Assert.Equal("CodeGroupBetriebBrush", badge.BrushKey);
        Assert.Equal("Betrieb", badge.Kurzlabel);
    }

    [Fact]
    public void Bestand_gruppe_BC()
    {
        var badge = CodeGroupBadgePolicy.Resolve("BCA");
        Assert.Equal("CodeGroupBestandBrush", badge.BrushKey);
        Assert.Equal("Bestand", badge.Kurzlabel);
    }

    [Fact]
    public void Unbekannte_gruppen_werden_sonstig()
    {
        Assert.Equal("CodeGroupSonstigBrush", CodeGroupBadgePolicy.Resolve("BDD").BrushKey);
        Assert.Equal("CodeGroupSonstigBrush", CodeGroupBadgePolicy.Resolve("Z99").BrushKey);
        Assert.Equal("CodeGroupSonstigBrush", CodeGroupBadgePolicy.Resolve("").BrushKey);
        Assert.Equal("CodeGroupSonstigBrush", CodeGroupBadgePolicy.Resolve(null).BrushKey);
    }

    [Fact]
    public void Kleinschreibung_wird_toleriert()
    {
        Assert.Equal("Struktur", CodeGroupBadgePolicy.Resolve("bab").Kurzlabel);
    }

    [Fact]
    public void Jede_gruppe_hat_glyph()
    {
        Assert.False(string.IsNullOrEmpty(CodeGroupBadgePolicy.Resolve("BAB").Glyph));
        Assert.False(string.IsNullOrEmpty(CodeGroupBadgePolicy.Resolve("XYZ").Glyph));
    }
}
