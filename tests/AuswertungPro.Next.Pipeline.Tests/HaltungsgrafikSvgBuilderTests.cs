using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer HaltungsgrafikSvgBuilder (IST-Verhalten).</summary>
public sealed class HaltungsgrafikSvgBuilderTests
{
    // --- Grundstruktur ---

    [Fact]
    public void BuildHaltungsgrafikSvg_erzeugt_gueltiges_svg_tag()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        Assert.StartsWith("<svg ", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_enthaelt_weissen_hintergrund()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        Assert.Contains("fill='#FFFFFF'", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_enthaelt_rohr_gradient()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        Assert.Contains("pipeGrad", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_standard_breite_770()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        Assert.Contains("width='770'", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_standard_hoehe_520()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        Assert.Contains("height='520'", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_override_hoehe_wird_verwendet()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null, overrideHeight: 700);
        Assert.Contains("height='700'", svg);
        Assert.DoesNotContain("height='520'", svg);
    }

    // --- Schachtknoten ---

    [Fact]
    public void BuildHaltungsgrafikSvg_enthaelt_oberen_schachtknoten()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, "Knoten-A", null, null);
        // Oberer Schacht ist immer vorhanden
        Assert.Contains("nodeShadow", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_start_knoten_name_im_svg()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, "SA-001", null, null);
        Assert.Contains("SA-001", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_end_knoten_name_im_svg()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, "SE-999", null);
        Assert.Contains("SE-999", svg);
    }

    // --- Fliessrichtung ---

    [Fact]
    public void BuildHaltungsgrafikSvg_fliessrichtung_unten_enthaelt_pfeil()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, flowDown: true);
        Assert.Contains("Fliessrichtung", svg);
        Assert.Contains("flowGrad", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_fliessrichtung_oben_enthaelt_pfeil()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, flowDown: false);
        Assert.Contains("Fliessrichtung", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_ohne_fliessrichtung_kein_pfeil()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, flowDown: null);
        Assert.DoesNotContain("Fliessrichtung", svg);
    }

    // --- Eintraege ---

    [Fact]
    public void BuildHaltungsgrafikSvg_punktschaden_erzeugt_symbol()
    {
        var entry = new ProtocolEntry { Code = "BAB", MeterStart = 10.0 };
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, new[] { entry }, null, null, null, null);
        // Crack-Symbol hat <path> mit stroke
        Assert.Contains("<path ", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_streckenschaden_erzeugt_schraffierung()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAF",
            MeterStart = 5.0,
            MeterEnd = 20.0,
            IsStreckenschaden = true
        };
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, new[] { entry }, null, null, null, null);
        Assert.Contains("dmgHatch", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_brand_farbe_erscheint_im_rohr_gradient()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null, brand: "#FF0000");
        Assert.Contains("#FF0000", svg);
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_sonderzeichen_in_knoten_name_werden_escaped()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, "Knoten & Test", null, null);
        Assert.Contains("Knoten &amp; Test", svg);
        Assert.DoesNotContain("Knoten & Test", svg.Replace("&amp;", ""));
    }

    [Fact]
    public void BuildHaltungsgrafikSvg_tick_markierungen_vorhanden()
    {
        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            50.0, Array.Empty<ProtocolEntry>(), null, null, null, null);
        // Tick-Beschriftungen enthalten Meterwerte
        Assert.Contains("0.00", svg);
    }
}
