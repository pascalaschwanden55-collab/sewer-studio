using System.Linq;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 5): Die Schachtgrafik ist ein senkrechter Schnitt (WinCan-Art)
/// mit Konus/Schachtwand/Sohle, Zu-/Ablauf-Stummeln und Schadenssymbolen je Zone. Diese Tests
/// sind WPF-frei und pruefen den reinen SVG-Bauer.
/// </summary>
public sealed class SchachtgrafikSvgBuilderTests
{
    [Fact]
    public void Das_erzeugte_SVG_ist_gueltiges_XML()
    {
        var (svg, _) = Baue();
        XDocument.Parse(svg);
    }

    [Fact]
    public void Ohne_Tiefe_entsteht_keine_Masslinie_sondern_ein_Hinweistext()
    {
        var (svg, _) = Baue(tiefeMeter: null);
        Assert.Contains("Tiefe nicht erfasst", svg);
    }

    [Fact]
    public void Mit_Tiefe_entsteht_eine_Masslinie_mit_dem_Wert_statt_des_Hinweistexts()
    {
        var (svg, _) = Baue(tiefeMeter: 2.4);
        Assert.Contains("2.40 m", svg);
        Assert.DoesNotContain("Tiefe nicht erfasst", svg);
    }

    [Fact]
    public void Fehlende_Innenmasse_zeigen_einen_Gedankenstrich()
    {
        var (svg, _) = Baue(dimension1Mm: null, dimension2Mm: null);
        Assert.Contains(">–<", svg);
    }

    /// <summary>Ein halbes Paar (nur eine Seite bekannt) waere eine falsche Aussage ueber die Form.</summary>
    [Fact]
    public void Ein_halbes_Massepaar_zeigt_ebenfalls_einen_Gedankenstrich()
    {
        var (svg, _) = Baue(dimension1Mm: "600", dimension2Mm: null);
        Assert.Contains(">–<", svg);
        Assert.DoesNotContain("600 ×", svg);
    }

    [Fact]
    public void Vollstaendige_Innenmasse_werden_angezeigt()
    {
        var (svg, _) = Baue(dimension1Mm: "1100", dimension2Mm: "900");
        Assert.Contains("1100 × 900", svg);
    }

    /// <summary>
    /// Fix-Runde 1 (Controller-Sichtprobe): hoechstens ZWEI Stummel je Seite werden beschriftet,
    /// der Rest steht als Zaehler — bei mehr als zwei ueberlappten sich die Beschriftungen in der
    /// schmalen Spalte.
    /// </summary>
    [Fact]
    public void Hoechstens_zwei_Stummel_je_Seite_werden_beschriftet_der_Rest_als_Zaehler()
    {
        var zulaeufe = Enumerable.Range(1, 5)
            .Select(i => new SchachtgrafikStummel($"H{i}", "DN200"))
            .ToList();

        var (svg, _) = Baue(zulaeufe: zulaeufe);

        Assert.Contains("H1 DN200", svg);
        Assert.Contains("H2 DN200", svg);
        Assert.DoesNotContain("H3", svg);
        Assert.DoesNotContain("H4", svg);
        Assert.DoesNotContain("H5", svg);
        Assert.Contains("+3", svg);
    }

    /// <summary>
    /// Fix-Runde 2 (Minor): Der "+n"-Zaehler bekommt eine eigene Hinweisflaeche mit den Namen
    /// der dadurch ausgeblendeten Haltungen — sonst bleiben sie ohne jeden Anhaltspunkt.
    /// </summary>
    [Fact]
    public void Der_Zaehler_fuer_ausgeblendete_Stummel_traegt_deren_Namen_im_Tooltip()
    {
        var zulaeufe = Enumerable.Range(1, 5)
            .Select(i => new SchachtgrafikStummel($"H{i}", "DN200"))
            .ToList();

        var (_, marken) = Baue(zulaeufe: zulaeufe);

        Assert.Contains(marken, m => m.Tooltip == "H3, H4, H5");
    }

    /// <summary>Zulauf und Ablauf werden unabhaengig voneinander gezeichnet.</summary>
    [Fact]
    public void Zulauf_und_Ablauf_erscheinen_beide_und_unabhaengig_je_Seite()
    {
        var (_, marken) = Baue(
            zulaeufe: [new SchachtgrafikStummel("H1", "DN300")],
            ablaeufe: [new SchachtgrafikStummel("H9", "DN250")]);

        Assert.Contains(marken, m => m.Tooltip == "H1 DN300");
        Assert.Contains(marken, m => m.Tooltip == "H9 DN250");
    }

    /// <summary>
    /// Fix-Runde 1: Eine lange Beschriftung ("Name DN200") wird mit Auslassungspunkten gekuerzt,
    /// bleibt aber im Hinweistext der Marke vollstaendig erhalten.
    /// </summary>
    [Fact]
    public void Eine_lange_Stummel_Beschriftung_wird_gekuerzt_der_Volltext_bleibt_im_Tooltip()
    {
        var stummel = new SchachtgrafikStummel("77457-77453", "DN300");
        var (svg, marken) = Baue(zulaeufe: [stummel]);

        Assert.DoesNotContain("77457-77453 DN300", svg);
        Assert.Contains("…", svg);
        Assert.Contains(marken, m => m.Tooltip == "77457-77453 DN300");
    }

    /// <summary>
    /// Ein Schaden je Zone (Konus, Schachtwand, Sohle) landet in der richtigen Zone: Die
    /// Hinweisflaechen liegen von oben nach unten in der physischen Reihenfolge der Zonen.
    /// </summary>
    [Fact]
    public void Ein_Schaden_je_Zone_erscheint_in_der_richtigen_vertikalen_Reihenfolge()
    {
        var schaeden = new[]
        {
            new SchachtgrafikSchadenEintrag(SchachtZone.Sohle, "default", "#D64541", "S1"),
            new SchachtgrafikSchadenEintrag(SchachtZone.Konus, "default", "#D64541", "K1"),
            new SchachtgrafikSchadenEintrag(SchachtZone.Schachtwand, "default", "#D64541", "W1"),
        };

        var (_, marken) = Baue(schaeden: schaeden);

        Assert.Equal(3, marken.Count);
        var reihenfolge = marken.OrderBy(m => m.Y).Select(m => m.Tooltip).ToArray();
        Assert.Equal(new[] { "K1", "W1", "S1" }, reihenfolge);
    }

    /// <summary>Anschluss-Schaeden teilen sich die Zeichenflaeche der Schachtwand (Anschluesse sitzen in der Wand).</summary>
    [Fact]
    public void Anschluss_Schaeden_liegen_zwischen_Konus_und_Sohle()
    {
        var schaeden = new[]
        {
            new SchachtgrafikSchadenEintrag(SchachtZone.Konus, "default", "#D64541", "K1"),
            new SchachtgrafikSchadenEintrag(SchachtZone.Anschluss, "default", "#D64541", "A1"),
            new SchachtgrafikSchadenEintrag(SchachtZone.Sohle, "default", "#D64541", "S1"),
        };

        var (_, marken) = Baue(schaeden: schaeden);

        var reihenfolge = marken.OrderBy(m => m.Y).Select(m => m.Tooltip).ToArray();
        Assert.Equal(new[] { "K1", "A1", "S1" }, reihenfolge);
    }

    /// <summary>Jede Hinweisflaeche traegt den fertigen Tooltip unveraendert weiter.</summary>
    [Fact]
    public void Jede_Hinweisflaeche_traegt_ihren_Tooltip()
    {
        var schaeden = new[] { new SchachtgrafikSchadenEintrag(SchachtZone.Sohle, "default", "#D64541", "BAB — Riss") };
        var (_, marken) = Baue(schaeden: schaeden);

        var marke = Assert.Single(marken);
        Assert.Equal("BAB — Riss", marke.Tooltip);
    }

    [Fact]
    public void Die_Schachtnummer_steht_im_Deckelbereich()
    {
        var (svg, _) = Baue(schachtnummer: "12345");
        Assert.Contains(">12345<", svg);
    }

    /// <summary>
    /// Fix-Runde 1 (Controller-Ruling): Ein Schacht ist kein Rohr, sondern ein Bauwerk mit
    /// realer Ausdehnung — der gezeichnete Schachtkoerper bleibt mindestens ein Drittel der
    /// Zeichenbreite breit.
    /// </summary>
    [Fact]
    public void Der_Schachtkoerper_ist_mindestens_ein_Drittel_der_Zeichenbreite_breit()
    {
        Assert.True(SchachtgrafikSvgBuilder.SchachtkoerperDurchmesser >= SchachtgrafikSvgBuilder.Width / 3d);
    }

    private static (string Svg, System.Collections.Generic.IReadOnlyList<SchachtgrafikMarke> Marken) Baue(
        string? schachtnummer = "S1",
        double? tiefeMeter = 2.4,
        string? dimension1Mm = "1100",
        string? dimension2Mm = "900",
        System.Collections.Generic.IReadOnlyList<SchachtgrafikStummel>? zulaeufe = null,
        System.Collections.Generic.IReadOnlyList<SchachtgrafikStummel>? ablaeufe = null,
        System.Collections.Generic.IReadOnlyList<SchachtgrafikSchadenEintrag>? schaeden = null)
        => SchachtgrafikSvgBuilder.Baue(
            schachtnummer,
            tiefeMeter,
            dimension1Mm,
            dimension2Mm,
            zulaeufe ?? [],
            ablaeufe ?? [],
            schaeden ?? []);
}
