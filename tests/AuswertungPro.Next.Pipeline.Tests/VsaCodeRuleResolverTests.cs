using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

// Charakterisierungs-Tests fuer VsaCodeRuleResolver (IST-Verhalten)
public sealed class VsaCodeRuleResolverTests
{
    // ── GetQuantRule ──────────────────────────────────────────────

    [Fact]
    public void GetQuantRule_gibt_null_fuer_unbekannten_code()
    {
        var (q1, q2) = VsaCodeRuleResolver.GetQuantRule("XXXX", null);
        Assert.Null(q1);
        Assert.Null(q2);
    }

    [Fact]
    public void GetQuantRule_gibt_regelmaessige_regel_fuer_baa()
    {
        // BAA hat Q1 Pflicht="P", Einheit="%"
        var (q1, q2) = VsaCodeRuleResolver.GetQuantRule("BAA", null);
        Assert.NotNull(q1);
        Assert.Equal("P", q1!.Pflicht);
        Assert.Equal("%", q1.Einheit);
        Assert.Null(q2);
    }

    [Fact]
    public void GetQuantRule_gibt_null_q1_fuer_baf_ohne_char1()
    {
        // BAF hat Q1=null explizit
        var (q1, _) = VsaCodeRuleResolver.GetQuantRule("BAF", null);
        Assert.Null(q1);
    }

    [Fact]
    public void GetQuantRule_loest_var_per_char1_auf()
    {
        // BAB hat Q1 Pflicht="V"; bei Char1="A" -> null (kein Wert)
        var (q1A, _) = VsaCodeRuleResolver.GetQuantRule("BAB", "A");
        Assert.Null(q1A);

        // Bei Char1="B" -> Pflicht="P", Einheit="mm"
        var (q1B, _) = VsaCodeRuleResolver.GetQuantRule("BAB", "B");
        Assert.NotNull(q1B);
        Assert.Equal("P", q1B!.Pflicht);
        Assert.Equal("mm", q1B.Einheit);
    }

    [Fact]
    public void GetQuantRule_gibt_q2_fuer_bca()
    {
        // BCA hat Q1 (Hoehe Anschluss) + Q2 (Breite)
        var (q1, q2) = VsaCodeRuleResolver.GetQuantRule("BCA", null);
        Assert.NotNull(q1);
        Assert.NotNull(q2);
        Assert.Equal("O", q2!.Pflicht);
    }

    [Theory]
    [InlineData("BAB", "B", "mm", 1, 200)]
    [InlineData("BAC", "A", "mm", 10, 1000)]
    [InlineData("BAD", "C", "mm", 20, 1000)]
    [InlineData("BAE", null, "mm", 5, 500)]
    [InlineData("BAG", null, "%", 1, 100)]
    [InlineData("BAI", "Z", "%", 1, 100)]
    [InlineData("BAJ", "A", "mm", 0, 9999)]
    [InlineData("BAJ", "C", "\u00b0", 1, 359)]
    [InlineData("BAK", "B", "%", 0, 100)]
    [InlineData("BAK", "F", "mm", 0, null)]
    [InlineData("BAL", "G", "mm", 0, null)]
    [InlineData("BBA", null, "%", 1, 100)]
    [InlineData("BBC", null, "%", 0, 100)]
    [InlineData("BBH", null, "Stk.", 0, 10000)]
    [InlineData("BCA", "A", "mm", 0, 10000)]
    [InlineData("BCC", "A", "\u00b0", 1, 359)]
    [InlineData("BDD", null, "%", 0, 100)]
    [InlineData("AEC", "B", "mm", 0, 4500)]
    public void GetQuantRule_verwendet_die_WinCan_VSA_2019_Einheit_und_Grenzen(
        string code,
        string? char1,
        string expectedUnit,
        int? expectedMin,
        int? expectedMax)
    {
        var (q1, _) = VsaCodeRuleResolver.GetQuantRule(code, char1);

        Assert.NotNull(q1);
        Assert.Equal(expectedUnit, q1!.Einheit);
        Assert.Equal(
            expectedMin.HasValue ? (double?)expectedMin.Value : null,
            q1.Min);
        Assert.Equal(
            expectedMax.HasValue ? (double?)expectedMax.Value : null,
            q1.Max);
    }

    [Fact]
    public void GetQuantRule_zeigt_profilbreite_nur_wenn_das_Profil_sie_benoetigt()
    {
        var (_, eggWidth) = VsaCodeRuleResolver.GetQuantRule("AEC", "B");
        var (_, circleWidth) = VsaCodeRuleResolver.GetQuantRule("AEC", "C");

        Assert.NotNull(eggWidth);
        Assert.Equal("mm", eggWidth!.Einheit);
        Assert.Equal(0, eggWidth.Min);
        Assert.Equal(4500, eggWidth.Max);
        Assert.Null(circleWidth);
    }

    [Fact]
    public void GetQuantRule_zeigt_keine_erfundene_Baulaengen_Quantifizierung()
    {
        var (q1, q2) = VsaCodeRuleResolver.GetQuantRule("AEF", null);

        Assert.Null(q1);
        Assert.Null(q2);
    }

    [Fact]
    public void Alle_sichtbaren_quantifizierungsregeln_haben_einheit_und_gueltige_grenzen()
    {
        foreach (var (code, rule) in VsaCodeTree.QuantRules)
        {
            var fields = new List<QuantField>();
            if (rule.Q1 is { Pflicht: not "V" } q1)
                fields.Add(q1);
            if (rule.Q1PerChar1 is not null)
                fields.AddRange(rule.Q1PerChar1.Values.OfType<QuantField>());
            if (rule.Q2 is { Pflicht: not "V" })
                fields.Add(rule.Q2);
            if (rule.Q2PerChar1 is not null)
                fields.AddRange(rule.Q2PerChar1.Values.OfType<QuantField>());

            foreach (var field in fields)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(field.Einheit),
                    $"{code}: Quantifizierung ohne sichtbare Einheit.");
                Assert.Contains(
                    field.Einheit,
                    new[] { "mm", "%", "\u00b0", "Stk." });
                Assert.True(
                    !field.Min.HasValue
                    || !field.Max.HasValue
                    || field.Min <= field.Max,
                    $"{code}: Minimum ist groesser als Maximum.");
            }
        }
    }

    // ── GetClockRule ──────────────────────────────────────────────

    [Fact]
    public void GetClockRule_gibt_none_fuer_steuer_codes()
    {
        Assert.Equal("none", VsaCodeRuleResolver.GetClockRule("BCD").Mode);
        Assert.Equal("none", VsaCodeRuleResolver.GetClockRule("BCE").Mode);
    }

    [Fact]
    public void GetClockRule_gibt_none_fuer_bogen()
    {
        Assert.Equal("none", VsaCodeRuleResolver.GetClockRule("BCC").Mode);
    }

    [Fact]
    public void GetClockRule_gibt_single_fuer_baj()
    {
        var rule = VsaCodeRuleResolver.GetClockRule("BAJ");
        Assert.Equal("single", rule.Mode);
    }

    [Fact]
    public void GetClockRule_gibt_default_range_fuer_unbekannte_codes()
    {
        var rule = VsaCodeRuleResolver.GetClockRule("BAB");
        Assert.Equal("range", rule.Mode);
    }

    // ── GetChar2Options ──────────────────────────────────────────

    [Fact]
    public void GetChar2Options_gibt_null_wenn_keine_char2_defniert()
    {
        // BAA hat kein Char2
        var cd = VsaCodeTree.Groups["BA"].Codes["BAA"];
        Assert.Null(VsaCodeRuleResolver.GetChar2Options(cd, "A"));
    }

    [Fact]
    public void GetChar2Options_gibt_char2_per_char1_wenn_vorhanden()
    {
        // BAI: Char2PerChar1["A"] definiert
        var cd = VsaCodeTree.Groups["BA"].Codes["BAI"];
        var opts = VsaCodeRuleResolver.GetChar2Options(cd, "A");
        Assert.NotNull(opts);
        Assert.True(opts!.ContainsKey("A")); // "verschoben"
    }

    [Fact]
    public void GetChar2Options_gibt_globales_char2_wenn_kein_char2_per_char1()
    {
        // BAB hat globales Char2 (A=laengs, B=radial etc.) und kein Char2PerChar1
        var cd = VsaCodeTree.Groups["BA"].Codes["BAB"];
        var opts = VsaCodeRuleResolver.GetChar2Options(cd, "A");
        Assert.NotNull(opts);
        Assert.Equal("laengs", opts!["A"]);
    }

    [Fact]
    public void GetChar2Options_gibt_chardef_char2_bei_chardef_definiertem_char2()
    {
        // BAKD = Faltenbildung hat eigenes Char2 auf CharDef-Ebene
        var cd = VsaCodeTree.Groups["BA"].Codes["BAK"];
        var opts = VsaCodeRuleResolver.GetChar2Options(cd, "D");
        Assert.NotNull(opts);
        Assert.Equal("laengs", opts!["A"]);
    }

    // ── IsInvalidCombo ────────────────────────────────────────────

    [Fact]
    public void IsInvalidCombo_gibt_false_wenn_allvalid_true()
    {
        // BAB hat AllValid=true -> niemals ungueltig
        var cd = VsaCodeTree.Groups["BA"].Codes["BAB"];
        Assert.False(VsaCodeRuleResolver.IsInvalidCombo(cd, "A", "Z"));
    }

    [Fact]
    public void IsInvalidCombo_gibt_true_fuer_bekannte_ungueltige_kombination()
    {
        // BAF: Char1="B", Char2="B" ist ungueltig
        var cd = VsaCodeTree.Groups["BA"].Codes["BAF"];
        Assert.True(VsaCodeRuleResolver.IsInvalidCombo(cd, "B", "B"));
    }

    [Fact]
    public void IsInvalidCombo_gibt_false_fuer_gueltige_kombination()
    {
        // BAF: Char1="A", Char2="A" ist gueltig (kein Invalid-Eintrag fuer "A")
        var cd = VsaCodeTree.Groups["BA"].Codes["BAF"];
        Assert.False(VsaCodeRuleResolver.IsInvalidCombo(cd, "A", "A"));
    }

    [Fact]
    public void IsInvalidCombo_gibt_false_wenn_kein_invalid_dictionary()
    {
        // BAA hat kein Invalid-Dictionary
        var cd = VsaCodeTree.Groups["BA"].Codes["BAA"];
        Assert.False(VsaCodeRuleResolver.IsInvalidCombo(cd, "A", "X"));
    }
}
