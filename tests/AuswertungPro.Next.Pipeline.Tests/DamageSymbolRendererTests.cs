using System.Text;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer DamageSymbolRenderer (IST-Verhalten).</summary>
public sealed class DamageSymbolRendererTests
{
    // Hilfsmethode: SVG-String fuer eine Kategorie erzeugen
    private static string Render(string category, string color = "#D64541", double cx = 50, double cy = 100, double s = 5)
    {
        var sb = new StringBuilder();
        DamageSymbolRenderer.RenderDamageSymbol(sb, cx, cy, category, color, s);
        return sb.ToString();
    }

    [Fact]
    public void RenderDamageSymbol_erzeugt_immer_weissen_hintergrund_kreis()
    {
        // Jede Kategorie soll einen weissen Hintergrundkreis enthalten
        foreach (var cat in new[] { "crack", "break", "deformation", "leak", "offset", "surface", "obstacle", "roots", "infiltration", "exfiltration", "incrustation", "deposit", "default" })
        {
            var result = Render(cat);
            Assert.Contains("fill='white'", result);
        }
    }

    [Fact]
    public void RenderDamageSymbol_crack_enthaelt_path_element()
    {
        var result = Render("crack");
        Assert.Contains("<path ", result);
        Assert.Contains("stroke=", result);
    }

    [Fact]
    public void RenderDamageSymbol_break_enthaelt_zwei_kreuzlinien()
    {
        var result = Render("break");
        // X-Kreuz besteht aus zwei <line>-Elementen (naechst dem weissen Hintergrundkreis)
        var lineCount = CountOccurrences(result, "<line ");
        Assert.Equal(2, lineCount);
    }

    [Fact]
    public void RenderDamageSymbol_deformation_enthaelt_ellipse()
    {
        var result = Render("deformation");
        Assert.Contains("<ellipse ", result);
    }

    [Fact]
    public void RenderDamageSymbol_leak_enthaelt_path_mit_fill()
    {
        var result = Render("leak", "#2196F3");
        Assert.Contains("<path ", result);
        Assert.Contains($"fill='#2196F3'", result);
    }

    [Fact]
    public void RenderDamageSymbol_roots_enthaelt_drei_linien()
    {
        var result = Render("roots");
        var lineCount = CountOccurrences(result, "<line ");
        Assert.Equal(3, lineCount);
    }

    [Fact]
    public void RenderDamageSymbol_default_enthaelt_polygon()
    {
        var result = Render("default");
        Assert.Contains("<polygon ", result);
    }

    [Fact]
    public void RenderDamageSymbol_farbe_wird_in_ausgabe_verwendet()
    {
        var result = Render("crack", "#ABCDEF");
        Assert.Contains("#ABCDEF", result);
    }

    [Fact]
    public void RenderDamageSymbol_koordinaten_erscheinen_im_output()
    {
        // cx=50, cy=100 -> werden als SVG-Koordinaten formatiert
        var result = Render("break", "#D64541", cx: 50, cy: 100);
        Assert.Contains("50", result);
        Assert.Contains("100", result);
    }

    [Fact]
    public void RenderDamageSymbol_deposit_ist_identisch_zu_incrustation()
    {
        var sb1 = new StringBuilder();
        DamageSymbolRenderer.RenderDamageSymbol(sb1, 0, 0, "deposit", "#8B6914");
        var sb2 = new StringBuilder();
        DamageSymbolRenderer.RenderDamageSymbol(sb2, 0, 0, "incrustation", "#8B6914");
        Assert.Equal(sb1.ToString(), sb2.ToString());
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
