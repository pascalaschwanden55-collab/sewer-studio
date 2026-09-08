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

    /// <summary>Hoechstens vier Stummel je Seite; der Rest steht als Zaehler.</summary>
    [Fact]
    public void Zulaeufe_werden_ab_fuenf_auf_vier_plus_Zaehler_gekuerzt()
    {
        var zulaeufe = Enumerable.Range(1, 5)
            .Select(i => new SchachtgrafikStummel($"H{i}", "DN200"))
            .ToList();

        var (svg, _) = Baue(zulaeufe: zulaeufe);

        for (var i = 1; i <= 4; i++)
            Assert.Contains($"H{i} DN200", svg);
        Assert.DoesNotContain("H5", svg);
        Assert.Contains("+1", svg);
    }

    /// <summary>Zulauf und Ablauf werden unabhaengig voneinander gezeichnet.</summary>
    [Fact]
    public void Zulauf_und_Ablauf_erscheinen_beide_und_unabhaengig_je_Seite()
    {
        var (svg, _) = Baue(
            zulaeufe: [new SchachtgrafikStummel("77457-77453", "DN300")],
            ablaeufe: [new SchachtgrafikStummel("77453-77449", "DN250")]);

        Assert.Contains("DN300", svg);
        Assert.Contains("DN250", svg);
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
