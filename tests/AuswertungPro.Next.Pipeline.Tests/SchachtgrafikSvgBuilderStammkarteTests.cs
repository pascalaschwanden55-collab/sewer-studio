using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Schachtgrafik Stammkarte (Schnitt + Grundriss) — WPF-frei. Die Zeichnung wird ueber den
/// SVG-Text und die Hinweisflaechen geprueft, nicht ueber Pixel.
/// </summary>
public sealed class SchachtgrafikSvgBuilderStammkarteTests
{
    private static SchachtgrafikZeichnung Zeichnung(bool mitTiefe = true, bool mitTabelle = true, bool mitLage = true, bool mitKoten = true)
        => SchachtgrafikSvgBuilder.Baue(SchachtgrafikBeispiel.Modell80409(mitTiefe, mitTabelle, mitLage, mitKoten));

    [Fact]
    public void Das_SVG_ist_gueltiges_XML_und_320_breit()
    {
        var z = Zeichnung();

        var doc = XDocument.Parse(z.Svg);
        Assert.Equal("svg", doc.Root!.Name.LocalName);
        Assert.Equal(SchachtgrafikSvgBuilder.Width, z.Breite);
        Assert.Equal(z.Hoehe.ToString(), doc.Root.Attribute("height")!.Value);
    }

    [Fact]
    public void Die_Hoehe_folgt_der_Tiefe()
    {
        var z = Zeichnung();

        var skala = Math.Clamp(SchachtgrafikSvgBuilder.SkalaZiel / 3.45, SchachtgrafikSvgBuilder.SkalaMin, SchachtgrafikSvgBuilder.SkalaMax);
        var erwartet = (int)Math.Ceiling(SchachtgrafikSvgBuilder.DeckelOkY + 3.45 * skala + SchachtgrafikSvgBuilder.RandUnten + SchachtgrafikSvgBuilder.GrundrissHoehe);
        Assert.Equal(erwartet, z.Hoehe);

        var tief = SchachtgrafikBeispiel.Schacht80409();
        tief.SetFieldValue("Schachttiefe", "6.00", FieldSource.Manual, true);
        var zTief = SchachtgrafikSvgBuilder.Baue(SchachtgrafikModellBuilder.Baue(tief, null, null, null, "#006E9C"));
        Assert.True(zTief.Hoehe > z.Hoehe, $"6 m ({zTief.Hoehe}) muss hoeher sein als 3.45 m ({z.Hoehe})");
    }

    [Fact]
    public void Die_Anschluesse_liegen_auf_ihrer_Tiefe()
    {
        var z = Zeichnung();

        // Kennungen je Typ: Tabellenzeile 3 (0.60 m) ist E2, Tabellenzeile 2 (3.38 m) ist E1.
        var hoch = z.Marken.First(m => m.Tooltip.StartsWith("E2 ", StringComparison.Ordinal));
        var tief = z.Marken.First(m => m.Tooltip.StartsWith("E1 ", StringComparison.Ordinal));
        var a1 = z.Marken.First(m => m.Tooltip.StartsWith("A1 ", StringComparison.Ordinal));
        var skala = Math.Clamp(SchachtgrafikSvgBuilder.SkalaZiel / 3.45, SchachtgrafikSvgBuilder.SkalaMin, SchachtgrafikSvgBuilder.SkalaMax);

        Assert.True(hoch.Y < tief.Y, "E2 (0.60 m) muss oberhalb von E1 (3.38 m) liegen");
        Assert.InRange(tief.Y - hoch.Y, (3.38 - 0.60) * skala - 25, (3.38 - 0.60) * skala + 25);
        Assert.True(a1.X > tief.X, "Der Auslauf steht rechts, der gegenueberliegende Einlauf links");
    }

    [Fact]
    public void Jeder_Schaden_hat_genau_eine_Hinweisflaeche_mit_seinem_Text()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();
        var z = SchachtgrafikSvgBuilder.Baue(modell);

        Assert.Equal(5, modell.Schaeden.Count);
        foreach (var schaden in modell.Schaeden)
            Assert.Single(z.Marken, m => m.Tooltip == schaden.Tooltip);

        // Marken: 5 Schaeden + 4 Kennungen im Schnitt + 3 Kennungen im Grundriss (E3, die vierte
        // Tabellenzeile, hat keine Richtung und wird dort nicht gezeichnet — kein erfundener Winkel).
        Assert.Equal(5 + 4 + 3, z.Marken.Count);
        Assert.Equal(1, z.Marken.Count(m => m.Tooltip.StartsWith("E3 ", StringComparison.Ordinal)));
    }

    [Fact]
    public void Ohne_Schaeden_und_Anschluesse_gibt_es_keine_Hinweisflaeche()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);

        var z = SchachtgrafikSvgBuilder.Baue(SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C"));

        Assert.Empty(z.Marken);
        Assert.Contains("Tiefe nicht erfasst", z.Svg, StringComparison.Ordinal);
        Assert.Contains("Innenmasse nicht erfasst", z.Svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Tiefe_gibt_es_keinen_Massstab_aber_den_Hinweis()
    {
        var z = Zeichnung(mitTiefe: true, mitKoten: false);
        Assert.Contains("m ab Deckel-OK", z.Svg, StringComparison.Ordinal);

        var ohne = Zeichnung(mitTiefe: false, mitKoten: false);
        Assert.DoesNotContain("m ab Deckel-OK", ohne.Svg, StringComparison.Ordinal);
        Assert.Contains("Tiefe nicht erfasst", ohne.Svg, StringComparison.Ordinal);
        Assert.Equal((int)Math.Ceiling(SchachtgrafikSvgBuilder.DeckelOkY + SchachtgrafikSvgBuilder.SchematischeTiefePx + SchachtgrafikSvgBuilder.RandUnten + SchachtgrafikSvgBuilder.GrundrissHoehe), ohne.Hoehe);
    }

    [Fact]
    public void Der_Grundriss_dreht_jedes_Rohr_und_zeigt_den_Nordpfeil_nur_mit_Koordinaten()
    {
        var mit = Zeichnung();
        var rohre = Regex.Matches(mit.Svg, @"<g transform='rotate\(").Count;
        Assert.True(rohre >= 4 + 1, $"4 Rohre, Auslaufpfeil und Nordpfeil erwartet, {rohre} Drehungen gefunden");
        Assert.Contains(">N</text>", mit.Svg, StringComparison.Ordinal);
        Assert.Contains("ohne Richtung: E3", mit.Svg, StringComparison.Ordinal);
        // Bei 6 Uhr liegt kein Rohr (E2 bei 114 Grad, E3 bei 231 Grad): Die Stundenmarke steht.
        Assert.Contains(">6</text>", mit.Svg, StringComparison.Ordinal);

        var ohne = Zeichnung(mitLage: false);
        Assert.DoesNotContain(">N</text>", ohne.Svg, StringComparison.Ordinal);
        Assert.Contains("Richtungen nicht erfasst: schematisch", ohne.Svg, StringComparison.Ordinal);
        // Schematisch liegt der groesste Einlauf E2 bei 6 Uhr; die Stundenmarke weicht seiner
        // Kennung (vorher las sich das als «Ë2»). 3 und 9 bleiben frei.
        Assert.DoesNotContain(">6</text>", ohne.Svg, StringComparison.Ordinal);
        Assert.Contains(">3</text>", ohne.Svg, StringComparison.Ordinal);
        Assert.Contains(">9</text>", ohne.Svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Legende_fuehrt_Anschluesse_Schaeden_Koten_und_Hinweise()
    {
        var z = Zeichnung();

        Assert.Contains(z.Legende, l => l.Marke == "A1" && l.Text.Contains("80409-80538", StringComparison.Ordinal) && l.Text.Contains("3.45 m", StringComparison.Ordinal));
        Assert.Contains(z.Legende, l => l.Marke == "E3" && l.Text.Contains("nicht im Projekt", StringComparison.Ordinal));
        Assert.Contains(z.Legende, l => l.Marke == "2" && l.Text.Contains("Bemerkung", StringComparison.Ordinal));
        Assert.Contains(z.Legende, l => l.Marke == "m" && l.Text.Contains("Deckel 498.62", StringComparison.Ordinal) && l.Text.Contains("A1 495.14", StringComparison.Ordinal));
        Assert.Contains(z.Legende, l => l.Marke == "·" && l.Text.Contains("Konus schematisch", StringComparison.Ordinal));
    }

    [Fact]
    public void Nur_Elemente_und_Attribute_der_Teilmenge()
    {
        var svg = Zeichnung().Svg;
        var ohneDefs = Regex.Replace(svg, "<defs>.*?</defs>", string.Empty, RegexOptions.Singleline);

        Assert.DoesNotMatch(@"<(circle|ellipse|rect)[^>]*stroke-dasharray", ohneDefs);
        Assert.DoesNotMatch(@"<g [^>]*(stroke|fill)=", ohneDefs);
        foreach (Match pfad in Regex.Matches(svg, @"<path[^>]* d='([^']*)'"))
        {
            var befehle = Regex.Replace(pfad.Groups[1].Value, @"[^A-Za-z]", string.Empty);
            Assert.Matches("^[MLCQZ]*$", befehle);
        }

        // Linien in Muted-/Akzentfarbe muessen nach der Skalierung 3 px behalten (Smoke-Regel).
        foreach (Match linie in Regex.Matches(ohneDefs, @"<line[^>]*>"))
        {
            var stroke = Regex.Match(linie.Value, @"stroke='(#[0-9A-Fa-f]{6})'").Groups[1].Value.ToUpperInvariant();
            if (stroke is "#6B7280" or "#4B5563" or "#8B7355" or "#006E9C" or "#2196F3" or "#1F6FEB")
            {
                var breite = double.Parse(Regex.Match(linie.Value, @"stroke-width='([0-9.]+)'").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(breite >= 4, $"Linie zu duenn: {linie.Value}");
            }
        }

        foreach (Match schrift in Regex.Matches(svg, @"font-size='([0-9.]+)'"))
            Assert.True(double.Parse(schrift.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 10);
    }

    [Fact]
    public void Ohne_Tabelle_kommen_die_Anschluesse_aus_den_Haltungen()
    {
        var z = Zeichnung(mitTabelle: false);

        Assert.Contains(z.Marken, m => m.Tooltip.StartsWith("A1 Auslauf · 80409-80538", StringComparison.Ordinal));
        Assert.Contains(z.Marken, m => m.Tooltip.StartsWith("E2 Einlauf · 80547-80409", StringComparison.Ordinal));
    }
}
