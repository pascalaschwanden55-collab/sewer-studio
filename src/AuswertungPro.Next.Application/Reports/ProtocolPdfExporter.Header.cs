using System;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Application.Reports;

// Header/Title-Bar/Wave-Divider/Grafik-Frame und SVG-Slice-Utilities fuer
// das Protokoll-PDF. Aus dem Hauptdatei extrahiert (Slice 1h).
public sealed partial class ProtocolPdfExporter
{
    internal static void ComposeTopHeader(IContainer container, byte[]? logoBytes, HaltungsprotokollPdfOptions options)
    {
        container.Column(outer =>
        {
            outer.Item().Row(row =>
            {
                row.ConstantItem(120).Height(38).AlignMiddle().Element(c =>
                {
                    if (logoBytes is not null)
                        c.Image(logoBytes).FitHeight();
                });

                row.RelativeItem().AlignRight().AlignBottom().Column(col =>
                {
                    var lines = options.SenderBlock?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    foreach (var line in lines)
                    {
                        col.Item().AlignRight().Text(line.Trim()).FontSize(8).FontColor("#475569");
                    }
                });
            });
            // Klare Doppellinie als Trenner: dicke Linie oben + dünne darunter -> formal, schweizerisch.
            outer.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor("#0F172A");
            outer.Item().PaddingTop(1).LineHorizontal(0.4f).LineColor("#0F172A");
        });
    }

    internal static void ComposeTitleBar(IContainer container, string title, string? subtitle, string brand)
    {
        // Hero-Style kompakt: vertikaler Brand-Block links + grosser Haltungs-Code + Subtitle-Tag rechts.
        var (mainTitle, microHead) = SplitTitleForHero(title);

        container
            .BorderBottom(0.5f).BorderColor("#0F172A")
            .PaddingBottom(5)
            .Row(row =>
            {
                row.ConstantItem(10).Background(brand);
                row.ConstantItem(10); // Spacer

                row.RelativeItem().Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(microHead))
                    {
                        col.Item().Text(microHead.ToUpperInvariant())
                            .FontSize(8).SemiBold().FontColor(brand);
                    }
                    col.Item()
                        .Text(mainTitle).FontSize(18).Bold().FontColor("#0F172A").LineHeight(1f);
                });

                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    row.AutoItem().AlignBottom().PaddingBottom(2).PaddingLeft(10)
                        .Border(0.5f).BorderColor(brand)
                        .PaddingVertical(2).PaddingHorizontal(6)
                        .Text(subtitle.ToUpperInvariant())
                        .FontSize(7).SemiBold().FontColor(brand);
                }
            });
    }

    /// <summary>Splittet einen Titel der Form "Haltungsprotokoll - 12.04.2026 - 175.1-408" in
    /// Mikrokopfzeile (Datum/Type) und Haupttitel (Haltungsname).</summary>
    private static (string Main, string? Micro) SplitTitleForHero(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ("Haltungsprotokoll", null);

        var parts = title.Split(" - ", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            return (parts[^1].Trim(), $"{parts[0].Trim()} · {parts[1].Trim()}");
        if (parts.Length == 2)
            return (parts[1].Trim(), parts[0].Trim());
        return (title.Trim(), null);
    }

    /// <summary>Wellen-Trennlinie als Abwasser-Uri-Brand-Element (passt zu Wasser/Abwasser).</summary>
    internal static void ComposeWaveDivider(IContainer container, string brand)
    {
        var hex = brand.TrimStart('#');
        // SVG-Welle: niedrig, ueber volle Breite, mit Brand-Farbe.
        // Zwei Wellen uebereinander fuer Tiefenwirkung (zweite leicht versetzt + transparenter).
        var waveSvg =
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 600 14' preserveAspectRatio='none'>" +
            $"<path d='M0 8 Q 30 2, 60 8 T 120 8 T 180 8 T 240 8 T 300 8 T 360 8 T 420 8 T 480 8 T 540 8 T 600 8' " +
            $"stroke='#{hex}' stroke-width='1.2' fill='none' opacity='0.85'/>" +
            $"<path d='M0 11 Q 30 5, 60 11 T 120 11 T 180 11 T 240 11 T 300 11 T 360 11 T 420 11 T 480 11 T 540 11 T 600 11' " +
            $"stroke='#{hex}' stroke-width='0.7' fill='none' opacity='0.4'/>" +
            "</svg>";
        container.Height(8).Svg(waveSvg).FitArea();
    }

    /// <summary>Sehr helle Brand-Variante fuer Hintergruende, die fast weiss wirken.</summary>
    internal static string ResolveNutzungsartBrandUltraLight(string brand) => brand switch
    {
        "#7A6242" => "#FBF8F3", // braun ultra-hell
        "#4A7FA5" => "#F4F8FB", // blau ultra-hell
        "#8E4A6E" => "#FBF6F8", // magenta ultra-hell
        _         => "#F8F9FA"  // neutral ultra-hell
    };

    /// <summary>
    /// Bestimmt die intrinsische SVG-Hoehe der Haltungsgrafik dynamisch basierend auf:
    /// - Anzahl Beobachtungen (Tabellenzeilen rechts: ~22pt pro Zeile)
    /// - Haltungslaenge (Pipe-Achse links: ~3.5pt pro Meter)
    /// Min 700pt (Standard), max 2400pt (sonst Slicing greift bei zu vielen Slices).
    /// Bei grafikHeight > 1000 triggert die Slicing-Logik im Render-Block.
    /// </summary>
    private static int ComputeDynamicGrafikHeight(double? length, int entryCount)
    {
        const int baseHeight = 700;
        const int minHeight = 700;
        const int maxHeight = 2400;
        const int rowHeight = 22;       // Tabellenzeile rechts
        const double pipeScale = 3.5;   // pt pro Meter Pipe-Achse links
        const int margins = 120;        // Header + Footer der Grafik

        var tableNeeded = entryCount * rowHeight + margins;
        var pipeNeeded = (int)Math.Ceiling((length ?? 0) * pipeScale) + margins;
        var desired = Math.Max(tableNeeded, pipeNeeded);

        // Wenn der dynamische Bedarf unter Standard liegt -> Standard nehmen.
        // Sonst auf Bedarf hochfahren und cappen.
        if (desired <= baseHeight)
            return baseHeight;

        return Math.Clamp(desired, minHeight, maxHeight);
    }

    /// <summary>Frame fuer die Haltungsgrafik (Border + Skala-Zeile + SVG-Container).</summary>
    private static void ComposeGrafikFrame(
        IContainer container,
        string svg,
        HaltungsgrafikScale? scale,
        float displayHeight)
    {
        container.Border(0.5f).BorderColor("#D1D5DB").Background("#FFFFFF").Padding(4).Column(g =>
        {
            if (scale is not null && (!string.IsNullOrWhiteSpace(scale.LengthText) || !string.IsNullOrWhiteSpace(scale.ScaleText)))
            {
                g.Item().Row(row =>
                {
                    row.RelativeItem().Text(scale.LengthText ?? "").FontSize(10).FontColor(Colors.Grey.Darken2);
                    row.AutoItem().Text(scale.ScaleText ?? "").FontSize(10).FontColor(Colors.Grey.Darken2);
                });
            }
            g.Item().Height(displayHeight).Svg(svg).FitArea();
        });
    }

    /// <summary>
    /// Extrahiert einen vertikalen Bereich [yStart..yStart+sliceHeight] aus einer SVG via viewBox-Manipulation.
    /// Aendert nur das aeusserste &lt;svg&gt;-Tag, der Inhalt bleibt unveraendert.
    /// Damit bleibt die Skala (Pixel pro SVG-Einheit) ueber alle Slices konstant.
    /// </summary>
    private static string ExtractSvgRange(string svg, int viewBoxWidth, int originalHeight, int yStart, int sliceHeight)
    {
        if (yStart < 0) yStart = 0;
        if (sliceHeight <= 0) return svg;
        if (yStart + sliceHeight > originalHeight) sliceHeight = originalHeight - yStart;

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var result = ReplaceFirstAttribute(svg, "viewBox", $"0 {yStart} {viewBoxWidth} {sliceHeight}");
        result = ReplaceFirstAttribute(result, "height", sliceHeight.ToString(ci));
        return result;
    }

    /// <summary>Convenience-Wrapper: splittet eine SVG in oberen und unteren Teil.</summary>
    private static (string Top, string Bottom) SliceSvgVertical(
        string svg, int viewBoxWidth, int originalHeight, double splitY)
    {
        if (splitY <= 0 || splitY >= originalHeight)
            return (svg, svg);

        var splitYInt = (int)Math.Round(splitY);
        var top = ExtractSvgRange(svg, viewBoxWidth, originalHeight, 0, splitYInt);
        var bottom = ExtractSvgRange(svg, viewBoxWidth, originalHeight, splitYInt, originalHeight - splitYInt);
        return (top, bottom);
    }

    private static string ReplaceFirstAttribute(string xml, string attrName, string newValue)
    {
        // Ersetzt das erste Vorkommen von attrName="..." (mit beliebigem Inhalt) durch attrName="newValue".
        var pattern = $@"{System.Text.RegularExpressions.Regex.Escape(attrName)}\s*=\s*""[^""]*""";
        var replacement = $"{attrName}=\"{newValue}\"";
        var rx = new System.Text.RegularExpressions.Regex(pattern);
        return rx.Replace(xml, replacement, 1);
    }
}
