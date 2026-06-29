using System.Collections.Generic;
using AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Charakterisierungs-Tests fuer VsaCodePathResolver.
/// Testet das genaue IST-Verhalten der Extraktion aus VsaCodeExplorerViewModel.
/// </summary>
public sealed class VsaCodePathResolverTests
{
    // ── Katalog-Aufbau ────────────────────────────────────────────────

    /// <summary>Minimaler Testkatalog: BA-Gruppe mit BAA (Verformung, Char1 A/B, Char2 A/B).</summary>
    private static IReadOnlyDictionary<string, GroupDef> BuildTestGroups()
    {
        var char2 = new Dictionary<string, string> { ["A"] = "laengs", ["B"] = "radial" };
        var baa = new VsaCodeDef
        {
            Label = "Verformung",
            Char1 = new Dictionary<string, CharDef>
            {
                ["A"] = new CharDef { Label = "vertikal", Char2 = char2 },
                ["B"] = new CharDef { Label = "horizontal" }
            }
        };
        // BAC hat XPrefix
        var bac = new VsaCodeDef
        {
            Label = "Bruch",
            XPrefix = true,
            Char1 = new Dictionary<string, CharDef>
            {
                ["A"] = new CharDef { Label = "partiell" }
            }
        };
        // BCD hat FinalCode (kein weiteres Navigieren)
        var bcd = new VsaCodeDef { Label = "Rohranfang", FinalCode = "BCD" };

        var group = new GroupDef(
            "Strukturschaden",
            "#DC2626",
            "BA",
            new Dictionary<string, VsaCodeDef>
            {
                ["BAA"] = baa,
                ["BAC"] = bac,
                ["BCD"] = bcd
            });

        return new Dictionary<string, GroupDef> { ["BA"] = group };
    }

    private static IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef cd, string c1Key)
    {
        if (cd.Char1 is not null
            && cd.Char1.TryGetValue(c1Key, out var charDef)
            && charDef.Char2 is not null)
            return charDef.Char2;
        return cd.Char2;
    }

    private VsaCodePathResolver CreateResolver()
        => new(BuildTestGroups(), GetChar2Options);

    // ── NormalizeCode ─────────────────────────────────────────────────

    [Fact]
    public void NormalizeCode_entfernt_punkte_und_stellt_gross()
    {
        Assert.Equal("BAAAB", VsaCodePathResolver.NormalizeCode("baa.a.b"));
    }

    [Fact]
    public void NormalizeCode_entfernt_ziffern_nicht()
    {
        Assert.Equal("BAA1", VsaCodePathResolver.NormalizeCode("baa1"));
    }

    [Fact]
    public void NormalizeCode_leere_eingabe_gibt_leerstring()
    {
        Assert.Equal(string.Empty, VsaCodePathResolver.NormalizeCode(null));
        Assert.Equal(string.Empty, VsaCodePathResolver.NormalizeCode("  "));
    }

    // ── BuildCode ────────────────────────────────────────────────────

    [Fact]
    public void BuildCode_ohne_char1_gibt_finalcode_oder_hauptcode()
    {
        var cdMitFinal = new VsaCodeDef { FinalCode = "BCD" };
        Assert.Equal("BCD", VsaCodePathResolver.BuildCode("BCD", cdMitFinal, null, null));

        var cdOhneFinal = new VsaCodeDef { Label = "Test" };
        Assert.Equal("BAA", VsaCodePathResolver.BuildCode("BAA", cdOhneFinal, null, null));
    }

    [Fact]
    public void BuildCode_mit_xprefix_und_char1()
    {
        var cd = new VsaCodeDef { XPrefix = true };
        Assert.Equal("BACXA", VsaCodePathResolver.BuildCode("BAC", cd, "A", null));
    }

    [Fact]
    public void BuildCode_mit_xprefix_und_char1_und_char2()
    {
        var cd = new VsaCodeDef { XPrefix = true };
        Assert.Equal("BACXAB", VsaCodePathResolver.BuildCode("BAC", cd, "A", "B"));
    }

    [Fact]
    public void BuildCode_ohne_xprefix()
    {
        var cd = new VsaCodeDef { XPrefix = false };
        Assert.Equal("BAAAB", VsaCodePathResolver.BuildCode("BAA", cd, "A", "B"));
    }

    // ── TryResolveCodePath – Erfolg ───────────────────────────────────

    [Fact]
    public void TryResolveCodePath_findet_endcode_bcd()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("BCD",
            out var grp, out var code, out var c1, out var c2, out var lvl, out var final);

        Assert.True(ok);
        Assert.Equal("BA", grp);
        Assert.Equal("BCD", code);
        Assert.Null(c1);
        Assert.Null(c2);
        Assert.Equal(1, lvl);
        Assert.Equal("BCD", final);
    }

    [Fact]
    public void TryResolveCodePath_findet_code_mit_char1_a()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("BAAA",
            out var grp, out var code, out var c1, out var c2, out var lvl, out var final);

        Assert.True(ok);
        Assert.Equal("BA", grp);
        Assert.Equal("BAA", code);
        Assert.Equal("A", c1);
        Assert.Null(c2);
        // A hat Char2 -> level 3, noch kein FinalCode
        Assert.Equal(3, lvl);
        Assert.Null(final);
    }

    [Fact]
    public void TryResolveCodePath_findet_code_mit_char1_b_kein_char2()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("BAAB",
            out var grp, out var code, out var c1, out var c2, out var lvl, out var final);

        Assert.True(ok);
        Assert.Equal("BAA", code);
        Assert.Equal("B", c1);
        Assert.Null(c2);
        // B hat kein Char2 -> level 2, FinalCode gesetzt
        Assert.Equal(2, lvl);
        Assert.Equal("BAAB", final);
    }

    [Fact]
    public void TryResolveCodePath_findet_vollstaendigen_code_mit_char2()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("BAAAB",
            out var grp, out var code, out var c1, out var c2, out var lvl, out var final);

        Assert.True(ok);
        Assert.Equal("BAA", code);
        Assert.Equal("A", c1);
        Assert.Equal("B", c2);
        Assert.Equal(3, lvl);
        Assert.Equal("BAAAB", final);
    }

    [Fact]
    public void TryResolveCodePath_xprefix_wird_uebersprungen()
    {
        var resolver = CreateResolver();
        // BAC hat XPrefix: BACXA ist der korrekte Code
        var ok = resolver.TryResolveCodePath("BACXA",
            out _, out var code, out var c1, out _, out var lvl, out var final);

        Assert.True(ok);
        Assert.Equal("BAC", code);
        Assert.Equal("A", c1);
        // Char1 A hat kein Char2 -> FinalCode, level 2
        Assert.Equal(2, lvl);
        Assert.Equal("BACXA", final);
    }

    [Fact]
    public void TryResolveCodePath_hauptcode_ohne_char1_ergibt_level2()
    {
        var resolver = CreateResolver();
        // BAA eingeben ohne Char1 -> level 2 (hat Char1)
        var ok = resolver.TryResolveCodePath("BAA",
            out _, out _, out var c1, out _, out var lvl, out var final);

        Assert.True(ok);
        Assert.Null(c1);
        Assert.Equal(2, lvl);
        Assert.Null(final); // noch kein Endcode
    }

    // ── TryResolveCodePath – Fehler ───────────────────────────────────

    [Fact]
    public void TryResolveCodePath_unbekannter_code_gibt_false()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("ZZZ", out _, out _, out _, out _, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryResolveCodePath_null_eingabe_gibt_false()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath(null, out _, out _, out _, out _, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryResolveCodePath_kleinschreibung_wird_normalisiert()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolveCodePath("baaab",
            out _, out var code, out var c1, out var c2, out _, out _);

        Assert.True(ok);
        Assert.Equal("BAA", code);
        Assert.Equal("A", c1);
        Assert.Equal("B", c2);
    }
}
