using AuswertungPro.Next.Domain.Protocol;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfObservationText;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Geometrie-Logik fuer die Label-Zeilen in der Haltungsgrafik:
/// Erstellt Labels aus Protokolleintraegen und verteilt sie kollisionsfrei
/// auf der verfuegbaren Zeichenflaeche.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral).
/// Abhaengigkeiten: <see cref="ProtocolPdfValueFormatting"/>, <see cref="ProtocolPdfObservationText"/>,
/// <see cref="ProtocolZustandText"/>, <see cref="DamageSymbolClassifier"/>.
/// </summary>
public static class HaltungsgrafikLabelLayout
{
    /// <summary>
    /// Erstellt Label-Objekte aus Protokolleintraegen mit berechneten SVG-Koordinaten.
    /// </summary>
    public static List<HaltungsgrafikLabel> BuildHaltungsgrafikLabels(
        IReadOnlyList<ProtocolEntry> entries,
        double length,
        double top,
        double bottom,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string brand = "#006E9C")
    {
        var list = new List<HaltungsgrafikLabel>();

        foreach (var entry in entries)
        {
            var isRange = entry.IsStreckenschaden && entry.MeterStart is not null && entry.MeterEnd is not null;
            var pos = isRange
                ? (entry.MeterStart!.Value + entry.MeterEnd!.Value) / 2d
                : entry.MeterStart ?? entry.MeterEnd;

            if (pos is null)
                continue;

            var y = MapToLine(pos.Value, length, top, bottom);
            var meterText = BuildObservationMeterStartText(entry);
            var codeText = string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim();
            var zustandText = ProtocolZustandText.BuildHaltungsgrafikZustandText(entry);
            var mpegText = BuildObservationMpegText(entry);
            var fotoText = ResolvePhotoNumberText(entry, photoNumbers);
            var stufeText = BuildObservationStufeText(entry);

            list.Add(new HaltungsgrafikLabel
            {
                TargetY = y,
                LabelY = y,
                MeterText = string.IsNullOrWhiteSpace(meterText) ? "-" : meterText,
                CodeText = string.IsNullOrWhiteSpace(codeText) ? "-" : codeText,
                ZustandText = string.IsNullOrWhiteSpace(zustandText) ? "-" : zustandText,
                MpegText = string.IsNullOrWhiteSpace(mpegText) ? "-" : mpegText,
                FotoText = string.IsNullOrWhiteSpace(fotoText) ? "-" : fotoText,
                StufeText = string.IsNullOrWhiteSpace(stufeText) ? "-" : stufeText,
                LineColor = isRange ? "#D64541" : DamageSymbolClassifier.GetDamageSymbolColor(
                    DamageSymbolClassifier.ResolveDamageSymbolCategory(entry.Code), brand)
            });
        }

        return list;
    }

    /// <summary>
    /// Verteilt Label-Zeilen kollisionsfrei im verfuegbaren Bereich [top, bottom]
    /// und setzt die FontSize abhaengig vom verfuegbaren Platz.
    /// </summary>
    public static void LayoutHaltungsgrafikLabels(
        List<HaltungsgrafikLabel> labels,
        double top,
        double bottom)
    {
        if (labels.Count == 0)
            return;

        labels.Sort((a, b) => a.TargetY.CompareTo(b.TargetY));
        var available = Math.Max(1d, bottom - top);
        var minGap = Math.Clamp(available / Math.Max(1, labels.Count), 9d, 15d);
        var minY = top + 2;
        var maxY = bottom - 2;

        labels[0].LabelY = Math.Clamp(labels[0].TargetY, minY, maxY);
        for (var i = 1; i < labels.Count; i++)
        {
            labels[i].LabelY = Math.Clamp(Math.Max(labels[i].TargetY, labels[i - 1].LabelY + minGap), minY, maxY);
        }

        var overflow = labels[^1].LabelY - maxY;
        if (overflow > 0)
        {
            for (var i = 0; i < labels.Count; i++)
                labels[i].LabelY -= overflow;
        }

        for (var i = labels.Count - 2; i >= 0; i--)
        {
            if (labels[i].LabelY > labels[i + 1].LabelY - minGap)
                labels[i].LabelY = labels[i + 1].LabelY - minGap;
        }

        var underflow = minY - labels[0].LabelY;
        if (underflow > 0)
        {
            for (var i = 0; i < labels.Count; i++)
                labels[i].LabelY += underflow;
        }

        for (var i = 0; i < labels.Count; i++)
            labels[i].LabelY = Math.Clamp(labels[i].LabelY, minY, maxY);

        var fontSize = minGap < 10 ? 9 : minGap < 12 ? 10 : 11;
        foreach (var label in labels)
            label.FontSize = fontSize;
    }

    // Aufloesung der Foto-Nummer fuer ein Label: zuerst aus der Foto-Map, Fallback BuildObservationPhotoText.
    private static string ResolvePhotoNumberText(
        ProtocolEntry entry,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers)
    {
        if (photoNumbers is null)
            return BuildObservationPhotoText(entry);

        if (photoNumbers.TryGetValue(entry, out var numbers))
            return numbers;

        return "-";
    }
}

/// <summary>
/// Daten-Container fuer eine Label-Zeile in der Haltungsgrafik.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral).
/// </summary>
public sealed class HaltungsgrafikLabel
{
    /// <summary>Tatsaechliche SVG-Y-Position des Protokolleintrags auf dem Rohr.</summary>
    public double TargetY { get; init; }

    /// <summary>Berechnete Y-Position der Label-Zeile (nach kollisionsfreier Verteilung).</summary>
    public double LabelY { get; set; }

    public string MeterText { get; init; } = "-";
    public string CodeText { get; init; } = "-";
    public string ZustandText { get; init; } = "-";
    public string MpegText { get; init; } = "-";
    public string FotoText { get; init; } = "-";
    public string StufeText { get; init; } = "-";

    /// <summary>Farbe der Bezugslinie vom Rohr zur Label-Zeile.</summary>
    public string LineColor { get; init; } = "#1F6FEB";

    /// <summary>SVG-Schriftgroesse fuer die Label-Zeile (abhaengig vom verfuegbaren Platz).</summary>
    public double FontSize { get; set; } = 9;
}
