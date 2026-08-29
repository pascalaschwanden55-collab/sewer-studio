using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer HaltungsgrafikLabelLayout (IST-Verhalten).</summary>
public sealed class HaltungsgrafikLabelLayoutTests
{
    // --- BuildHaltungsgrafikLabels ---

    [Fact]
    public void BuildHaltungsgrafikLabels_leer_gibt_leere_liste()
    {
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            Array.Empty<ProtocolEntry>(), 50.0, top: 10, bottom: 400, photoNumbers: null);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_eintrag_ohne_meter_wird_ignoriert()
    {
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = null, MeterEnd = null };
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry }, 50.0, top: 10, bottom: 400, photoNumbers: null);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_eintrag_mit_meter_erzeugt_label()
    {
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 10.0 };
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry }, 50.0, top: 10, bottom: 410, photoNumbers: null);
        Assert.Single(result);
        Assert.Equal("BAB", result[0].CodeText);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_mit_katalog_zeigt_primaertitel_und_bemerkung()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            MeterStart = 2.9,
            Beschreibung = "Anschluss von 12 Uhr in Schmutzleitung"
        };

        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry },
            2.9,
            top: 10,
            bottom: 400,
            photoNumbers: null,
            brand: "#006E9C",
            catalog: VsaResolverTestCatalog.CreateDefault());

        Assert.Equal(
            "Rohrende, Anschluss von 12 Uhr in Schmutzleitung",
            Assert.Single(result).ZustandText);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_targetY_ist_proportional_zur_laenge()
    {
        // Meter 25 in Haltung 50m => Mitte zwischen top und bottom
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 25.0 };
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry }, 50.0, top: 0, bottom: 400, photoNumbers: null);
        Assert.Single(result);
        Assert.Equal(200.0, result[0].TargetY, precision: 1);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_streckenschaden_nutzt_mittelpunkt()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAF",
            MeterStart = 10.0,
            MeterEnd = 30.0,
            IsStreckenschaden = true
        };
        // Mittelpunkt bei 20m in einer 50m-Haltung: 20/50 = 0.4 * 400 = 160
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry }, 50.0, top: 0, bottom: 400, photoNumbers: null);
        Assert.Single(result);
        Assert.Equal(160.0, result[0].TargetY, precision: 1);
    }

    [Fact]
    public void BuildHaltungsgrafikLabels_ohne_code_liefert_strich_als_code_text()
    {
        var entry = new ProtocolEntry { Code = "", MeterStart = 5.0 };
        var result = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            new[] { entry }, 50.0, top: 0, bottom: 400, photoNumbers: null);
        Assert.Single(result);
        Assert.Equal("-", result[0].CodeText);
    }

    // --- LayoutHaltungsgrafikLabels ---

    [Fact]
    public void LayoutHaltungsgrafikLabels_leer_keine_exception()
    {
        var labels = new List<HaltungsgrafikLabel>();
        // Sollte keine Exception werfen
        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 400);
        Assert.Empty(labels);
    }

    [Fact]
    public void LayoutHaltungsgrafikLabels_ein_label_bleibt_in_bereich()
    {
        var label = new HaltungsgrafikLabel { TargetY = 200, LabelY = 200 };
        var labels = new List<HaltungsgrafikLabel> { label };
        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 400);
        Assert.InRange(label.LabelY, 10.0, 400.0);
    }

    [Fact]
    public void LayoutHaltungsgrafikLabels_labels_sind_aufsteigend_geordnet()
    {
        // Labels in umgekehrter Reihenfolge -> nach Layout aufsteigend
        var labels = new List<HaltungsgrafikLabel>
        {
            new() { TargetY = 300, LabelY = 300 },
            new() { TargetY = 100, LabelY = 100 },
            new() { TargetY = 200, LabelY = 200 }
        };
        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 400);
        for (var i = 1; i < labels.Count; i++)
            Assert.True(labels[i].LabelY >= labels[i - 1].LabelY,
                $"Label {i} LabelY={labels[i].LabelY} muss >= {labels[i - 1].LabelY} sein");
    }

    [Fact]
    public void LayoutHaltungsgrafikLabels_alle_label_y_in_gueltigem_bereich()
    {
        var labels = new List<HaltungsgrafikLabel>();
        for (var i = 0; i < 20; i++)
            labels.Add(new HaltungsgrafikLabel { TargetY = i * 20, LabelY = i * 20 });

        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 400);
        foreach (var l in labels)
            Assert.InRange(l.LabelY, 10.0, 400.0);
    }

    [Fact]
    public void LayoutHaltungsgrafikLabels_font_size_wird_bei_engen_labels_kleiner()
    {
        // Bei 50 Labels in 100px Bereich -> minimale FontSize
        var labels = new List<HaltungsgrafikLabel>();
        for (var i = 0; i < 50; i++)
            labels.Add(new HaltungsgrafikLabel { TargetY = 10 + i * 2, LabelY = 10 + i * 2 });

        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 110);
        // FontSize sollte 9 (minimal) sein bei engem Platz
        Assert.All(labels, l => Assert.Equal(9.0, l.FontSize));
    }

    [Fact]
    public void LayoutHaltungsgrafikLabels_font_size_ist_mindestens_9()
    {
        var labels = new List<HaltungsgrafikLabel>
        {
            new() { TargetY = 200, LabelY = 200 }
        };
        HaltungsgrafikLabelLayout.LayoutHaltungsgrafikLabels(labels, top: 10, bottom: 400);
        Assert.True(labels[0].FontSize >= 9.0);
    }
}
