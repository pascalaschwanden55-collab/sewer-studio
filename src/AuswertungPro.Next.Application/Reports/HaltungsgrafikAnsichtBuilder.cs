using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Ein anklickbarer Bereich ueber der Haltungsgrafik: die Stelle einer Beobachtung auf dem
/// Rohr samt fertigem Hinweistext. Die Koordinaten sind SVG-Koordinaten der Grafik.
/// </summary>
public sealed record HaltungsgrafikMarke(double X, double Y, double Breite, double Hoehe, string Tooltip);

/// <summary>Fertige Haltungsgrafik: SVG-Text, seine Masse und die Hinweisflaechen.</summary>
public sealed record HaltungsgrafikAnsicht(
    string Svg,
    int Breite,
    int Hoehe,
    IReadOnlyList<HaltungsgrafikMarke> Marken);

/// <summary>
/// Baut die Haltungsgrafik des Haltungsprotokolls fuer die Anzeige — mit denselben Argumenten
/// wie der PDF-Weg in <c>ProtocolPdfExporter.BuildHaltungsprotokollPdf</c>: aufgeloeste
/// Eintraege, Haltungslaenge, nicht inspizierte Bereiche, Schachtknoten und Fliessrichtung.
/// WPF-frei, damit die Regel testbar bleibt und die Oberflaeche nur noch zeichnet.
///
/// Fotonummern bleiben bewusst leer: Sie stammen im PDF aus dem Fototeil, den es in der
/// Uebersicht nicht gibt. Eine erfundene Nummer waere schlimmer als ein Gedankenstrich.
/// </summary>
public static class HaltungsgrafikAnsichtBuilder
{
    /// <summary>Feste Breite der Grafik (wie im PDF).</summary>
    public const int Breite = HaltungsgrafikSvgBuilder.Width;

    /// <summary>
    /// Markenfarbe der Grafik. Bewusst der Standardwert des Builders: Die Oberflaeche bildet
    /// genau diesen Wert auf ihren Akzent-Token ab, damit in der Grafik keine feste Farbe steht.
    /// </summary>
    public const string Markenfarbe = "#006E9C";

    private const double MarkeGroesse = 16d;
    private const int HoeheMinimum = 240;
    private const int HoeheMaximum = 1400;

    /// <summary>
    /// Baut die Grafik der Haltung. Gibt <c>null</c> zurueck, wenn keine belastbare Laenge
    /// vorliegt — ohne Laenge gibt es keinen Massstab, und eine geratene Laenge waere eine
    /// erfundene Angabe.
    /// </summary>
    public static HaltungsgrafikAnsicht? Baue(
        HaltungRecord? record,
        ICodeCatalogProvider? catalog,
        int hoehe,
        bool? flowDownVorgabe = null)
    {
        if (record is null)
            return null;

        hoehe = Math.Clamp(hoehe, HoeheMinimum, HoeheMaximum);

        var aufgeloest = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, record.Protocol ?? new ProtocolDocument());
        var laenge = ProtocolPdfEntryResolver.ResolveHoldingLength(record, aufgeloest);
        if (laenge is not > 0)
            return null;

        var eintraege = CounterInspectionStationingNormalizer.NormalizeForExport(aufgeloest, laenge)
            .OrderBy(e => e.MeterStart ?? e.MeterEnd ?? double.MaxValue)
            .ToList();
        var luecken = InspectionGapDetector.DetectUnknownGaps(eintraege, laenge);
        var (start, ende) = Knoten(record);
        var flowDown = flowDownVorgabe
                       ?? HoldingNodeParser.ParseFlowDirection(record.GetFieldValue("Inspektionsrichtung"));

        var svg = HaltungsgrafikSvgBuilder.BuildHaltungsgrafikSvg(
            laenge.Value,
            eintraege,
            photoNumbers: null,
            start,
            ende,
            flowDown,
            Markenfarbe,
            hoehe,
            luecken,
            catalog);

        return new HaltungsgrafikAnsicht(svg, Breite, hoehe, Marken(eintraege, laenge.Value, hoehe, catalog));
    }

    /// <summary>
    /// Schachtknoten: zuerst die erfassten Felder, sonst der Haltungsname wie im PDF-Weg.
    /// Ein leeres Feld darf den Namen nicht verdecken.
    /// </summary>
    private static (string? Start, string? Ende) Knoten(HaltungRecord record)
    {
        var (ausName, endeAusName) = HoldingNodeParser.SplitHoldingNodes(record.GetFieldValue("Haltungsname"));
        var oben = record.GetFieldValue("Schacht_oben");
        var unten = record.GetFieldValue("Schacht_unten");
        return (
            string.IsNullOrWhiteSpace(oben) ? ausName : oben.Trim(),
            string.IsNullOrWhiteSpace(unten) ? endeAusName : unten.Trim());
    }

    /// <summary>
    /// Hinweisflaechen ueber den Symbolen auf dem Rohr. Lage und Texte stammen aus derselben
    /// Label-Rechnung wie die Beschriftungstabelle der Grafik — kein zweiter Rechenweg.
    /// </summary>
    private static IReadOnlyList<HaltungsgrafikMarke> Marken(
        IReadOnlyList<ProtocolEntry> eintraege,
        double laenge,
        int hoehe,
        ICodeCatalogProvider? catalog)
    {
        var top = (double)HaltungsgrafikSvgBuilder.MarginTop
                  + HaltungsgrafikSvgBuilder.HeaderHeight
                  + HaltungsgrafikSvgBuilder.NodeZone;
        var bottom = hoehe - HaltungsgrafikSvgBuilder.MarginBottom;

        var labels = HaltungsgrafikLabelLayout.BuildHaltungsgrafikLabels(
            eintraege, laenge, top, bottom, photoNumbers: null, Markenfarbe, catalog);

        return labels
            .Select(label => new HaltungsgrafikMarke(
                HaltungsgrafikSvgBuilder.LineX - MarkeGroesse / 2d,
                label.TargetY - MarkeGroesse / 2d,
                MarkeGroesse,
                MarkeGroesse,
                Hinweistext(label)))
            .ToList();
    }

    private static string Hinweistext(HaltungsgrafikLabel label)
    {
        var text = label.CodeText;
        if (!string.IsNullOrWhiteSpace(label.ZustandText) && label.ZustandText != "-")
            text += " — " + label.ZustandText;
        if (!string.IsNullOrWhiteSpace(label.MeterText) && label.MeterText != "-")
            text += $" ({label.MeterText})";
        return text;
    }
}
