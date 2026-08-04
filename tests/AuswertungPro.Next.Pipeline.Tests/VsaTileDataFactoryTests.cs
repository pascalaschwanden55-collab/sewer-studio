using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Charakterisierungs-Tests fuer VsaTileDataFactory.
/// Prueft die konsolidierte Badge-Logik aus den 4 Populate*Column-Methoden.
/// </summary>
public sealed class VsaTileDataFactoryTests
{
    // ── GetQuantBadge ────────────────────────────────────────────────

    [Fact]
    public void GetQuantBadge_null_gibt_null_null()
    {
        var (text, color) = VsaTileDataFactory.GetQuantBadge(null);
        Assert.Null(text);
        Assert.Null(color);
    }

    [Fact]
    public void GetQuantBadge_pflicht_P_gibt_rote_farbe()
    {
        var q = new QuantField { Pflicht = "P", Einheit = "%" };
        var (text, color) = VsaTileDataFactory.GetQuantBadge(q);
        Assert.Equal("%", text);
        Assert.Equal(VsaTileDataFactory.PflichtColor, color);
    }

    [Fact]
    public void GetQuantBadge_optional_gibt_orange_farbe()
    {
        var q = new QuantField { Pflicht = "O", Einheit = "mm" };
        var (text, color) = VsaTileDataFactory.GetQuantBadge(q);
        Assert.Equal("mm", text);
        Assert.Equal(VsaTileDataFactory.QuantColor, color);
    }

    [Fact]
    public void GetQuantBadge_ohne_einheit_gibt_fallback_Q()
    {
        var q = new QuantField { Pflicht = "O", Einheit = null };
        var (text, _) = VsaTileDataFactory.GetQuantBadge(q);
        Assert.Equal("Q", text);
    }

    // ── ForGroup ─────────────────────────────────────────────────────

    [Fact]
    public void ForGroup_befuellt_felder_korrekt()
    {
        var grp = new GroupDef("Strukturschaden", "#DC2626", "BA", new());
        var tile = VsaTileDataFactory.ForGroup("BA", grp);

        Assert.Equal("BA", tile.Key);
        Assert.Equal("BA", tile.Label);
        Assert.Equal("Strukturschaden", tile.Description);
        Assert.Equal("#DC2626", tile.GroupColor);
        Assert.Equal("BA", tile.Icon);
        Assert.False(tile.IsSelected);
    }

    [Fact]
    public void ForGroup_selected_flag_wird_gesetzt()
    {
        var grp = new GroupDef("Test", "#000", "X", new());
        var tile = VsaTileDataFactory.ForGroup("X", grp, isSelected: true);
        Assert.True(tile.IsSelected);
    }

    // ── ForCode ──────────────────────────────────────────────────────

    [Fact]
    public void ForCode_endcode_setzt_isfinal_true()
    {
        var cd = new VsaCodeDef { Label = "Rohranfang", FinalCode = "BCD" };
        var tile = VsaTileDataFactory.ForCode("BCD", cd, null, "#2563EB");
        Assert.True(tile.IsFinal);
    }

    [Fact]
    public void ForCode_kein_finalcode_setzt_isfinal_false()
    {
        var cd = new VsaCodeDef { Label = "Verformung" };
        var tile = VsaTileDataFactory.ForCode("BAA", cd, null, "#DC2626");
        Assert.False(tile.IsFinal);
    }

    [Fact]
    public void ForCode_pflicht_q1_gibt_rotes_badge()
    {
        var cd = new VsaCodeDef { Label = "Riss" };
        var q1 = new QuantField { Pflicht = "P", Einheit = "mm" };
        var tile = VsaTileDataFactory.ForCode("BAB", cd, q1, "#DC2626");
        Assert.Equal("mm", tile.BadgeText);
        Assert.Equal(VsaTileDataFactory.PflichtColor, tile.BadgeColor);
    }

    // ── ForChar1 ─────────────────────────────────────────────────────

    [Fact]
    public void ForChar1_ohne_c2_setzt_isfinal_true()
    {
        var charDef = new CharDef { Label = "horizontal" };
        var tile = VsaTileDataFactory.ForChar1("B", charDef, "BAA", false, hasC2: false, null, "#DC2626");
        Assert.True(tile.IsFinal);
        Assert.Equal("BAAB", tile.Label);
    }

    [Fact]
    public void ForChar1_mit_c2_setzt_isfinal_false()
    {
        var charDef = new CharDef { Label = "vertikal" };
        var tile = VsaTileDataFactory.ForChar1("A", charDef, "BAA", false, hasC2: true, null, "#DC2626");
        Assert.False(tile.IsFinal);
    }

    [Fact]
    public void ForChar1_mit_c2_zeigt_semantischen_navigationsklartext()
    {
        var charDef = new CharDef { Label = "Bogen nach links" };
        var tile = VsaTileDataFactory.ForChar1(
            "A",
            charDef,
            "BCC",
            false,
            hasC2: true,
            null,
            "#2563EB",
            catalogLabel: "Bogen nach links",
            parentCatalogLabel: "Bogen");

        Assert.Equal("Bogen nach links", tile.Description);
    }

    [Fact]
    public void ForChar1_xprefix_erscheint_im_label()
    {
        var charDef = new CharDef { Label = "partiell" };
        var tile = VsaTileDataFactory.ForChar1("A", charDef, "BAC", true, hasC2: false, null, "#DC2626");
        Assert.Equal("BACXA", tile.Label);
    }

    // ── ForChar2 ─────────────────────────────────────────────────────

    [Fact]
    public void ForChar2_ist_immer_final()
    {
        // Aufbau: codeKey="BAA" + char1Key="A" + key="A" => "BAAAA"
        var tile = VsaTileDataFactory.ForChar2("A", "laengs", "BAA", "A", false, false, "#DC2626");
        Assert.True(tile.IsFinal);
        Assert.Equal("BAAAA", tile.Label);
        Assert.Equal("laengs", tile.Description);
    }

    [Fact]
    public void ForChar2_invalid_combo_setzt_isinvalid()
    {
        var tile = VsaTileDataFactory.ForChar2("Z", "ungueltig", "BAA", "A", false, isInvalid: true, "#DC2626");
        Assert.True(tile.IsInvalid);
    }

    [Fact]
    public void ForChar2_xprefix_erscheint_im_label()
    {
        var tile = VsaTileDataFactory.ForChar2("A", "label", "BAC", "A", true, false, "#DC2626");
        Assert.Equal("BACXAA", tile.Label);
    }

    [Fact]
    public void ForChar2_zeigt_den_exakten_finalen_katalogklartext()
    {
        var tile = VsaTileDataFactory.ForChar2(
            "A",
            "oben",
            "BCC",
            "A",
            false,
            false,
            "#2563EB",
            catalogLabel: "Bogen nach links oben",
            parentCatalogLabel: "Bogen nach links");

        Assert.Equal("Bogen nach links oben", tile.Description);
    }

    // ── F4: ForChar1 altes Verhalten – kein "Q"-Fallback bei fehlender Einheit ────────────

    [Fact]
    public void ForChar1_q1_ohne_einheit_gibt_kein_Q_badge()
    {
        // Alter Pfad: ForChar1 zeigte q1?.Einheit direkt (null bei fehlender Einheit),
        // NICHT den "Q"-Fallback, den GetQuantBadge() liefert.
        var charDef = new CharDef { Label = "laengs" };
        var q1 = new QuantField { Pflicht = "O", Einheit = null };

        var tile = VsaTileDataFactory.ForChar1("A", charDef, "BAB", false, hasC2: false, q1, "#DC2626");

        Assert.Null(tile.BadgeText);   // kein "Q"-Fallback bei fehlender Einheit
    }

    [Fact]
    public void ForChar1_q1_mit_einheit_zeigt_einheit_im_badge()
    {
        // Altes und neues Verhalten identisch wenn Einheit gesetzt: Einheit erscheint als Badge
        var charDef = new CharDef { Label = "laengs" };
        var q1 = new QuantField { Pflicht = "P", Einheit = "mm" };

        var tile = VsaTileDataFactory.ForChar1("A", charDef, "BAB", false, hasC2: false, q1, "#DC2626");

        Assert.Equal("mm", tile.BadgeText);
        Assert.Equal(VsaTileDataFactory.PflichtColor, tile.BadgeColor);
    }
}
