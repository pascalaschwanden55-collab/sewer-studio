using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using AuswertungPro.Next.Application.Dossiers.Preview;

using AuswertungPro.Next.Application.Dossiers;

namespace AuswertungPro.Next.Infrastructure.Dossiers.Preview;

/// <summary>
/// Loest auf, wie ein Absatz und seine Textstuecke in der Vorlage wirklich
/// aussehen: Schriftart, Groesse, Fettung, Zeilen- und Absatzabstand, Einzug
/// und Ausrichtung.
///
/// Word verteilt diese Angaben auf drei Ebenen — die Vorgaben des Dokuments,
/// die Formatvorlage samt ihrer Elternvorlagen, und die direkte Auszeichnung am
/// Absatz. Wer nur die letzte liest, bekommt fuer die meisten Absaetze gar
/// nichts und zeichnet alles in derselben Groesse.
/// </summary>
internal sealed class DocxFormatResolver
{
    /// <summary>Ein Twip ist 1/1440 Zoll; ein Bildpunkt 1/96 Zoll.</summary>
    private const double TwipsProPixel = 15.0;

    private readonly Dictionary<string, Style> _styles;
    private readonly RunProperties? _standardRun;
    private readonly ParagraphProperties? _standardAbsatz;

    /// <summary>
    /// Die Formatvorlage mit <c>w:default="1"</c>. Word wendet sie auf jeden
    /// Absatz an, der keine eigene nennt — genau das ist auf dem Deckblatt der
    /// Fall. Ohne sie faellt die Schrift auf die Dokumentvorgabe zurueck, und
    /// das ganze Deckblatt erschiene in Times statt Arial.
    /// </summary>
    private readonly string? _standardStilId;

    public DocxFormatResolver(MainDocumentPart mainPart)
    {
        ArgumentNullException.ThrowIfNull(mainPart);

        var teil = mainPart.StyleDefinitionsPart?.Styles;

        _styles = teil?.Elements<Style>()
            .Where(s => s.StyleId?.Value is not null)
            .GroupBy(s => s.StyleId!.Value!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, Style>(StringComparer.Ordinal);

        _standardRun = teil?.DocDefaults?.RunPropertiesDefault?.RunPropertiesBaseStyle is { } rpr
            ? Umwandeln(rpr)
            : null;

        _standardAbsatz = teil?.DocDefaults?.ParagraphPropertiesDefault?.ParagraphPropertiesBaseStyle is { } ppr
            ? Umwandeln(ppr)
            : null;

        _standardStilId = teil?.Elements<Style>()
            .FirstOrDefault(st => st.Type?.Value == StyleValues.Paragraph
                && st.Default?.Value == true)
            ?.StyleId?.Value;
    }

    private static RunProperties Umwandeln(RunPropertiesBaseStyle quelle)
        => new(quelle.OuterXml);

    private static ParagraphProperties Umwandeln(ParagraphPropertiesBaseStyle quelle)
        => new(quelle.OuterXml);

    public static double TwipsZuPixel(double twips) => twips / TwipsProPixel;

    public static double EmuZuPixel(double emu) => emu / 9525.0;

    /// <summary>Halbe Punkte in Bildpunkte: 24 halbe Punkte = 12 pt = 16 px.</summary>
    public static double HalbePunkteZuPixel(double halbePunkte) => halbePunkte * 2.0 / 3.0;

    /// <summary>
    /// Die Kette aus Vorgaben, Formatvorlage und direkter Auszeichnung — von
    /// aussen nach innen, damit das Naehere gewinnt.
    /// </summary>
    private IEnumerable<OpenXmlElement?> Kette(Paragraph absatz)
    {
        yield return _standardAbsatz;
        yield return _standardRun;

        var stilId = absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? _standardStilId;

        foreach (var stil in Stilkette(stilId))
        {
            yield return stil.StyleParagraphProperties;
            yield return stil.StyleRunProperties;
        }

        yield return absatz.ParagraphProperties;
        yield return absatz.ParagraphProperties?.ParagraphMarkRunProperties;
    }

    /// <summary>Von der aeltesten Elternvorlage bis zur benannten.</summary>
    private IEnumerable<Style> Stilkette(string? styleId)
    {
        var kette = new List<Style>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(styleId)
               && gesehen.Add(styleId)
               && _styles.TryGetValue(styleId, out var stil))
        {
            kette.Add(stil);
            styleId = stil.BasedOn?.Val?.Value;
        }

        kette.Reverse();
        return kette;
    }

    public DossierPreviewParagraphFormat AbsatzFormat(Paragraph absatz)
    {
        ArgumentNullException.ThrowIfNull(absatz);

        double vor = 0;
        double nach = 0;
        double? zeilenhoehe = null;
        double links = 0;
        double rechts = 0;
        double erste = 0;
        var ausrichtung = DossierPreviewAlignment.Left;
        var ueberschrift = false;

        var groesse = SchriftgroessePx(absatz, null);

        foreach (var ebene in Kette(absatz))
        {
            if (ebene is null)
                continue;

            if (ebene.Elements<SpacingBetweenLines>().FirstOrDefault() is { } abstand)
            {
                if (Zahl(abstand.Before?.Value) is { } v)
                    vor = TwipsZuPixel(v);

                if (Zahl(abstand.After?.Value) is { } n)
                    nach = TwipsZuPixel(n);

                if (Zahl(abstand.Line?.Value) is { } z)
                {
                    // "auto" zaehlt in 240steln einer Zeile, alles andere in Twips.
                    var automatisch = abstand.LineRule?.Value is null
                        || abstand.LineRule.Value == LineSpacingRuleValues.Auto;

                    zeilenhoehe = automatisch
                        ? (Math.Abs(z - 240) < 0.5 ? null : groesse * 1.2 * z / 240.0)
                        : TwipsZuPixel(z);
                }
            }

            if (ebene.Elements<Indentation>().FirstOrDefault() is { } einzug)
            {
                if (Zahl(einzug.Left?.Value) is { } l)
                    links = TwipsZuPixel(l);

                if (Zahl(einzug.Right?.Value) is { } r)
                    rechts = TwipsZuPixel(r);

                if (Zahl(einzug.FirstLine?.Value) is { } f)
                    erste = TwipsZuPixel(f);

                if (Zahl(einzug.Hanging?.Value) is { } h)
                    erste = -TwipsZuPixel(h);
            }

            if (ebene.Elements<Justification>().FirstOrDefault()?.Val?.Value is { } jc)
            {
                ausrichtung =
                    jc == JustificationValues.Center ? DossierPreviewAlignment.Center
                    : jc == JustificationValues.Right ? DossierPreviewAlignment.Right
                    : jc == JustificationValues.Both ? DossierPreviewAlignment.Justify
                    : DossierPreviewAlignment.Left;
            }

            if (ebene.Elements<OutlineLevel>().FirstOrDefault()?.Val?.Value is { } stufe && stufe < 9)
                ueberschrift = true;
        }

        var styleId = absatz.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        if (IstUeberschriftenStil(styleId))
            ueberschrift = true;

        var titel = styleId.Equals("Titel", StringComparison.OrdinalIgnoreCase)
            || styleId.Equals("Title", StringComparison.OrdinalIgnoreCase);

        var verzeichnis = DossierTocStyle.IsEntry(styleId);

        return new DossierPreviewParagraphFormat(
            vor, nach, zeilenhoehe,
            new DossierPreviewEdges(links, 0, rechts, 0) with { Top = erste },
            ausrichtung,
            ueberschrift,
            titel,
            verzeichnis);
    }

    internal static bool IstUeberschriftenStil(string styleId)
        => DossierHeadingStyle.IsHeading(styleId);

    public DossierPreviewRunFormat RunFormat(Paragraph absatz, Run? run)
    {
        ArgumentNullException.ThrowIfNull(absatz);

        var schrift = "Arial";
        var fett = false;
        var kursiv = false;
        var unterstrichen = false;
        string? farbe = null;

        void Lies(OpenXmlElement? ebene)
        {
            if (ebene is null)
                return;

            if (ebene.Elements<RunFonts>().FirstOrDefault()?.Ascii?.Value is { } name
                && name.Length > 0)
            {
                schrift = name;
            }

            if (ebene.Elements<Bold>().FirstOrDefault() is { } b)
                fett = b.Val?.Value ?? true;

            if (ebene.Elements<Italic>().FirstOrDefault() is { } i)
                kursiv = i.Val?.Value ?? true;

            if (ebene.Elements<Underline>().FirstOrDefault()?.Val?.Value is { } u
                && u != UnderlineValues.None)
            {
                unterstrichen = true;
            }

            if (ebene.Elements<Color>().FirstOrDefault()?.Val?.Value is { } c
                && !string.Equals(c, "auto", StringComparison.OrdinalIgnoreCase))
            {
                farbe = c;
            }
        }

        foreach (var ebene in Kette(absatz))
            Lies(ebene);

        Lies(run?.RunProperties);

        return new DossierPreviewRunFormat(
            schrift,
            SchriftgroessePx(absatz, run),
            fett,
            kursiv,
            unterstrichen,
            farbe);
    }

    private double SchriftgroessePx(Paragraph absatz, Run? run)
    {
        double halbePunkte = 20;

        foreach (var ebene in Kette(absatz))
        {
            if (ebene?.Elements<FontSize>().FirstOrDefault()?.Val?.Value is { } wert
                && Zahl(wert) is { } zahl)
            {
                halbePunkte = zahl;
            }
        }

        if (run?.RunProperties?.FontSize?.Val?.Value is { } eigen && Zahl(eigen) is { } eigene)
            halbePunkte = eigene;

        return HalbePunkteZuPixel(halbePunkte);
    }

    private static double? Zahl(string? wert)
        => double.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl)
            ? zahl
            : null;
}
