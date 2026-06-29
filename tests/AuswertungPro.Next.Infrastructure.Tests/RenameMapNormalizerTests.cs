using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer RenameMapNormalizer (pure static Helfer).
/// </summary>
public sealed class RenameMapNormalizerTests
{
    // ── NormalizeToken ─────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeToken_Null_GibtLeerenString()
    {
        var result = RenameMapNormalizer.NormalizeToken(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizeToken_Leerzeichen_WirdGetrimmt()
    {
        var result = RenameMapNormalizer.NormalizeToken("  ABC  ");
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void NormalizeToken_NurLeerzeichen_GibtLeerenString()
    {
        var result = RenameMapNormalizer.NormalizeToken("   ");
        Assert.Equal("", result);
    }

    // ── ResolveValue ───────────────────────────────────────────────────────────

    [Fact]
    public void ResolveValue_LeereMap_GibtWertUnveraendertZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = RenameMapNormalizer.ResolveValue(map, "ABC");
        Assert.Equal("ABC", result);
    }

    [Fact]
    public void ResolveValue_EinfacheKette_KollabiertKorrekt()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C"
        };
        var result = RenameMapNormalizer.ResolveValue(map, "A");
        Assert.Equal("C", result);
    }

    [Fact]
    public void ResolveValue_ZyklusGeschuetzt_HaengtNicht()
    {
        // A -> B -> A = Zyklus; visited bricht Schleife, Algorithmus kehrt zum Startknoten zurueck
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "A"
        };
        var result = RenameMapNormalizer.ResolveValue(map, "A");
        // IST-Verhalten: A->B->A (visited["B"]=true), naechster Versuch A->B liefert visited.Add("A")=false -> break -> "A"
        Assert.Equal("A", result);
    }

    [Fact]
    public void ResolveValue_NichtInMap_GibtWertUnveraendertZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X"] = "Y"
        };
        var result = RenameMapNormalizer.ResolveValue(map, "Z");
        Assert.Equal("Z", result);
    }

    [Fact]
    public void ResolveValue_LeeresValue_GibtLeerenString()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = RenameMapNormalizer.ResolveValue(map, "   ");
        Assert.Equal("", result);
    }

    // ── NormalizeMap ───────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeMap_SelbstEintrag_WirdEntfernt()
    {
        // A -> A ist sinnlos
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "A"
        };
        var changed = RenameMapNormalizer.NormalizeMap(map);
        Assert.True(changed);
        Assert.Empty(map);
    }

    [Fact]
    public void NormalizeMap_KetteKollabiert_UndEintragAktualisiert()
    {
        // A -> B, B -> C  => nach Normalize: A -> C, B -> C
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "C"
        };
        RenameMapNormalizer.NormalizeMap(map);
        Assert.Equal("C", map["A"]);
        Assert.Equal("C", map["B"]);
    }

    [Fact]
    public void NormalizeMap_LeerZielWertWirdEntfernt()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "   "  // Leerzeichen-Ziel
        };
        var changed = RenameMapNormalizer.NormalizeMap(map);
        Assert.True(changed);
        Assert.Empty(map);
    }

    [Fact]
    public void NormalizeMap_UnveraenderteMap_GibtFalseZurueck()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B"
        };
        var changed = RenameMapNormalizer.NormalizeMap(map);
        Assert.False(changed);
        Assert.Equal("B", map["A"]);
    }

    [Fact]
    public void NormalizeMap_ZyklusBleibtErhalten()
    {
        // A -> B -> A: ResolveValue landet wieder beim Ausgangswert -> kein Self-Match auf Schluessel-Ebene
        // -> IST-Verhalten: Map unveraendert, beide Eintraege bleiben
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = "B",
            ["B"] = "A"
        };
        var changed = RenameMapNormalizer.NormalizeMap(map);
        Assert.False(changed);
        Assert.True(map.ContainsKey("A"));
        Assert.True(map.ContainsKey("B"));
    }
}
