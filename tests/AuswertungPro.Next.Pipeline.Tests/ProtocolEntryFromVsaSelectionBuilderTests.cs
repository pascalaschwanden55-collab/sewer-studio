using System;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

/// <summary>
/// Charakterisierungs-Tests fuer ProtocolEntryFromVsaSelectionBuilder.
/// Testet das genaue IST-Verhalten der Logik aus VsaCodeExplorerViewModel.BuildProtocolEntry.
/// </summary>
public sealed class ProtocolEntryFromVsaSelectionBuilderTests
{
    // ── BuildBeschreibung ─────────────────────────────────────────────

    [Fact]
    public void BuildBeschreibung_nur_label()
    {
        var result = ProtocolEntryFromVsaSelectionBuilder.BuildBeschreibung("Verformung", null);
        Assert.Equal("Verformung", result);
    }

    [Fact]
    public void BuildBeschreibung_label_und_sublabel_verbunden_mit_bindestrich()
    {
        var result = ProtocolEntryFromVsaSelectionBuilder.BuildBeschreibung("Verformung", "vertikal");
        Assert.Equal("Verformung - vertikal", result);
    }

    [Fact]
    public void BuildBeschreibung_leer_wenn_kein_label()
    {
        var result = ProtocolEntryFromVsaSelectionBuilder.BuildBeschreibung("", null);
        Assert.Equal("", result);
    }

    // ── NormalizeClockValues: Modus none ─────────────────────────────

    [Fact]
    public void NormalizeClockValues_modus_none_gibt_null_null()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("none", "6", "9");
        Assert.Null(von);
        Assert.Null(bis);
    }

    // ── NormalizeClockValues: Modus single ───────────────────────────

    [Fact]
    public void NormalizeClockValues_single_setzt_bis_auf_00()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("single", "6", null);
        Assert.Equal("06", von);
        Assert.Equal("00", bis);
    }

    [Fact]
    public void NormalizeClockValues_single_ohne_von_gibt_null_null()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("single", null, null);
        Assert.Null(von);
        Assert.Null(bis);
    }

    // ── NormalizeClockValues: Modus range ────────────────────────────

    [Fact]
    public void NormalizeClockValues_range_bis_leer_setzt_auto_00()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("range", "3", "");
        Assert.Equal("03", von);
        Assert.Equal("00", bis);
    }

    [Fact]
    public void NormalizeClockValues_range_mit_von_und_bis()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("range", "3", "9");
        Assert.Equal("03", von);
        Assert.Equal("09", bis);
    }

    [Fact]
    public void NormalizeClockValues_range_ungueltige_werte_werden_null()
    {
        var (von, bis) = ProtocolEntryFromVsaSelectionBuilder.NormalizeClockValues("range", "99", "abc");
        Assert.Null(von);
        Assert.Null(bis);
    }

    // ── Build: Grundfelder ────────────────────────────────────────────

    [Fact]
    public void Build_setzt_code_und_beschreibung()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAAAB",
            FinalLabel = "Verformung",
            FinalSublabel = "vertikal",
            MeterStart = "12.50"
        };

        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);

        Assert.Equal("BAAAB", entry.Code);
        Assert.Equal("Verformung - vertikal", entry.Beschreibung);
    }

    [Fact]
    public void Build_parst_meterstart_korrekt()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "23,50" };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal(23.5, entry.MeterStart);
    }

    [Fact]
    public void Build_meterend_null_wenn_leer()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "5" };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Null(entry.MeterEnd);
    }

    [Fact]
    public void Build_parst_meterend_wenn_angegeben()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "5", MeterEnd = "8.5" };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal(8.5, entry.MeterEnd);
    }

    [Fact]
    public void Build_parst_zeit_korrekt()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "0", Zeit = "01:30" };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal(TimeSpan.FromSeconds(90), entry.Zeit);
    }

    [Fact]
    public void Build_isstreckenschaden_wird_uebertragen()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "0", IsStreckenschaden = true };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.True(entry.IsStreckenschaden);
    }

    // ── Build: CodeMeta-Parameter ─────────────────────────────────────

    [Fact]
    public void Build_codemeta_speichert_q1_und_q2()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            Q1Value = "50",
            Q2Value = "3"
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal("50", entry.CodeMeta!.Parameters["vsa.q1"]);
        Assert.Equal("3", entry.CodeMeta.Parameters["vsa.q2"]);
    }

    [Fact]
    public void Build_leere_q_werte_werden_entfernt()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "0", Q1Value = "" };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.False(entry.CodeMeta!.Parameters.ContainsKey("vsa.q1"));
    }

    [Fact]
    public void Build_clock_mode_none_entfernt_uhr_parameter()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            ClockMode = "none",
            ClockVon = "6",
            ClockBis = "9"
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.False(entry.CodeMeta!.Parameters.ContainsKey("vsa.uhr.von"));
        Assert.False(entry.CodeMeta.Parameters.ContainsKey("vsa.uhr.bis"));
    }

    [Fact]
    public void Build_clock_mode_single_setzt_bis_auf_00()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            ClockMode = "single",
            ClockVon = "6"
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal("06", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("00", entry.CodeMeta.Parameters["vsa.uhr.bis"]);
    }

    [Fact]
    public void Build_clock_mode_range_auto_bis_00()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            ClockMode = "range",
            ClockVon = "3",
            ClockBis = ""
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal("03", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("00", entry.CodeMeta.Parameters["vsa.uhr.bis"]);
    }

    [Fact]
    public void Build_rohrverbindung_true_setzt_parameter_1()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            AnRohrverbindung = true
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal("1", entry.CodeMeta!.Parameters["vsa.rohrverbindung"]);
    }

    [Fact]
    public void Build_rohrverbindung_false_entfernt_parameter()
    {
        var input = new VsaSelectionInput { FinalCode = "BAA", MeterStart = "0", AnRohrverbindung = false };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.False(entry.CodeMeta!.Parameters.ContainsKey("vsa.rohrverbindung"));
    }

    [Fact]
    public void Build_fotos_werden_uebertragen()
    {
        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            FotoPaths = new[] { "/a/b.png", "/c/d.png" }
        };
        var entry = ProtocolEntryFromVsaSelectionBuilder.Build(input);
        Assert.Equal(2, entry.FotoPaths.Count);
        Assert.Equal("/a/b.png", entry.FotoPaths[0]);
    }

    // ── Build: Existing Entry aktualisieren ───────────────────────────

    [Fact]
    public void Build_mit_existing_entry_aktualisiert_in_place()
    {
        var existing = new ProtocolEntry { Code = "ALTCODE" };
        var id = existing.EntryId;

        var input = new VsaSelectionInput { FinalCode = "NEUERCODE", MeterStart = "1" };
        var result = ProtocolEntryFromVsaSelectionBuilder.Build(input, existing);

        Assert.Same(existing, result);
        Assert.Equal(id, result.EntryId);
        Assert.Equal("NEUERCODE", result.Code);
    }

    [Fact]
    public void Build_fotos_werden_ersetzt_nicht_angehaengt()
    {
        var existing = new ProtocolEntry();
        existing.FotoPaths.Add("/alt.png");

        var input = new VsaSelectionInput
        {
            FinalCode = "BAA",
            MeterStart = "0",
            FotoPaths = new[] { "/neu.png" }
        };
        var result = ProtocolEntryFromVsaSelectionBuilder.Build(input, existing);

        Assert.Single(result.FotoPaths);
        Assert.Equal("/neu.png", result.FotoPaths[0]);
    }
}
