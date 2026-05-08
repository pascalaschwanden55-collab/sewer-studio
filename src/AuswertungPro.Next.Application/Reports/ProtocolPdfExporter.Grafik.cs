using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// Haltungsgrafik-Block des Protokoll-PDF: Zustand-Text-Builder, Label-Layout,
// Massstab-/Tick-Berechnung. BuildHaltungsgrafikSvg selbst bleibt im Hauptfile
// (~400 Zeilen SVG-Pipeline). Slice 1c.
public sealed partial class ProtocolPdfExporter
{
    private static string BuildHaltungsgrafikZustandText(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        if (string.IsNullOrWhiteSpace(desc))
            return "-";

        return Shorten(desc, 120);
    }

    private static string BuildObservationZustandTextLong(ProtocolEntry entry)
    {
        var desc = NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (string.IsNullOrWhiteSpace(desc))
            desc = BuildParameterShortText(entry);
        if (string.IsNullOrWhiteSpace(desc))
            desc = entry.CodeMeta?.Notes?.Trim();

        return string.IsNullOrWhiteSpace(desc) ? "-" : desc;
    }

    private static string NormalizeZustandDescription(string? raw, string? code)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var text = raw.Trim();
        var codeToken = code?.Trim();

        // If pattern is "CODE @0.00m (desc)" -> take the inside.
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open >= 0 && close > open)
        {
            var prefix = text.Substring(0, open);
            if ((!string.IsNullOrWhiteSpace(codeToken) && prefix.Contains(codeToken, StringComparison.OrdinalIgnoreCase))
                || Regex.IsMatch(prefix, @"@\s*\d"))
            {
                text = text.Substring(open + 1, close - open - 1);
            }
        }

        if (!string.IsNullOrWhiteSpace(codeToken))
            text = Regex.Replace(text, @"^\s*" + Regex.Escape(codeToken) + @"\b\s*", "", RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"^\s*@?\s*\d+(?:[.,]\d+)?\s*m\b\s*", "", RegexOptions.IgnoreCase);
        // Nur isolierte Kuerzel (z.B. "BCD", "BBCC") am Anfang entfernen, keine normalen Woerter
        text = Regex.Replace(text, @"^\s*[A-Z0-9]{1,6}(?:\s+[A-Z0-9]{1,6})?(?=\s|$)", "", RegexOptions.None);

        // Import-Artefakte: Trailing Hash/ID-Fragmente entfernen
        // Beispiele: "-80631_6e c06c5c-c9", "137124-fc", "80fd46-", "f5fa69-828"
        text = Regex.Replace(text, @"\s+-?\d+_[0-9a-fA-F]+(?:\s+[0-9a-fA-F-]+)*\s*$", "");
        text = Regex.Replace(text, @"\s+[0-9a-fA-F]{5,}-[0-9a-fA-F]*\s*$", "");

        // Klartext: Redundante Phrasen kuerzen
        text = Regex.Replace(text, @"\s*Richtungs[aä]nderung\b", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"^Anderer Grund f[uü]r Abbruch der Inspektion,?\s*", "", RegexOptions.IgnoreCase);

        text = text.Trim(' ', '-', '–', ':', ',', '/');

        return text;
    }

    private sealed class HaltungsgrafikLabel
    {
        public double TargetY { get; init; }
        public double LabelY { get; set; }
        public string MeterText { get; init; } = "-";
        public string CodeText { get; init; } = "-";
        public string ZustandText { get; init; } = "-";
        public string MpegText { get; init; } = "-";
        public string FotoText { get; init; } = "-";
        public string StufeText { get; init; } = "-";
        public string LineColor { get; init; } = "#1F6FEB";
        public double FontSize { get; set; } = 9;
    }

    private static List<HaltungsgrafikLabel> BuildHaltungsgrafikLabels(
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
            var zustandText = BuildHaltungsgrafikZustandText(entry);
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
                LineColor = isRange ? "#D64541" : GetDamageSymbolColor(ClassifyDamageSymbol(entry), brand)
            });
        }

        return list;
    }

    private static void LayoutHaltungsgrafikLabels(
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

    private sealed record HaltungsgrafikScale(string? LengthText, string? ScaleText);

    private static HaltungsgrafikScale BuildHaltungsgrafikScale(double? length, int? svgHeight = null)
    {
        if (!length.HasValue || length.Value <= 0)
            return new HaltungsgrafikScale(null, null);

        var ratio = ComputeScaleRatio(length.Value, svgHeight);
        var lengthText = $"Haltungslänge: {length.Value:0.00} m";
        var scaleText = ratio.HasValue ? $"Massstab: 1:{ratio.Value}" : "";
        return new HaltungsgrafikScale(lengthText, scaleText);
    }

    private static int? ComputeScaleRatio(double length, int? svgHeight = null)
    {
        if (length <= 0)
            return null;

        var effectiveHeight = svgHeight ?? HaltungsgrafikHeight;
        var plotHeight = effectiveHeight - HaltungsgrafikMarginTop - HaltungsgrafikMarginBottom - HaltungsgrafikHeaderHeight - HaltungsgrafikNodeZone;
        var plotCm = plotHeight * 2.54 / 72.0;
        if (plotCm <= 0.01)
            return null;

        var mPerCm = length / plotCm;
        if (mPerCm <= 0)
            return null;

        return (int)Math.Round(mPerCm * 100.0, MidpointRounding.AwayFromZero);
    }

    private static List<double> BuildTicks(double length, double step)
    {
        var list = new List<double>();
        if (length <= 0 || step <= 0)
            return list;

        var m = 0d;
        while (m <= length + 1e-6)
        {
            list.Add(m);
            m += step;
        }

        if (list.Count == 0 || Math.Abs(list[^1] - length) > 1e-6)
            list.Add(length);

        return list.Distinct().OrderBy(x => x).ToList();
    }

    private static double ChooseTickStep(double length)
    {
        var candidates = new[] { 0.2, 0.5, 1d, 2d, 5d, 10d, 20d, 50d };
        if (length <= 0)
            return 1;

        foreach (var step in candidates)
        {
            var count = length / step;
            if (count >= 4 && count <= 8)
                return step;
        }

        return candidates.Last();
    }
}
