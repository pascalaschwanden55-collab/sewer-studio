using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// SVG-Pipeline der Haltungsgrafik: BuildHaltungsgrafikSvg ist der ~400-Zeilen-
// Renderer (Defs/Header/Ticks/Rohr/Knoten/Schaeden/Anschluesse/Labels). Plus
// 3 fachliche Code-Klassifikatoren (Abort/Lateral/ClockHour) die nur hier
// gebraucht werden. Slice 1e.
public sealed partial class ProtocolPdfExporter
{
    private static string BuildHaltungsgrafikSvg(
        double length,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? startNode,
        string? endNode,
        bool? flowDown,
        string brand = "#006E9C",
        int? overrideHeight = null)
    {
        var width = HaltungsgrafikWidth;
        var height = overrideHeight ?? HaltungsgrafikHeight;
        var marginTop = HaltungsgrafikMarginTop;
        var headerHeight = HaltungsgrafikHeaderHeight;
        var nodeZone = HaltungsgrafikNodeZone;
        var marginBottom = HaltungsgrafikMarginBottom;
        var lineX = HaltungsgrafikLineX;
        var tableX = HaltungsgrafikTableX;
        var rightMargin = HaltungsgrafikRightMargin;

        // top/bottom: Rohr-Anfang/-Ende mit Abstand fuer Schachtknoten und Header
        var top = (double)marginTop + headerHeight + nodeZone;
        var bottom = height - marginBottom;
        var pipeWidth = 14d;
        var pipeHalf = pipeWidth / 2.0;

        var tableWidth = Math.Max(1d, width - tableX - rightMargin);
        var colMeterWidth = 54d;
        var colCodeWidth = 56d;
        var colMpegWidth = 62d;
        var colFotoWidth = 38d;
        var colStufeWidth = 40d;
        var colZustandWidth = Math.Max(120d, tableWidth - (colMeterWidth + colCodeWidth + colMpegWidth + colFotoWidth + colStufeWidth));

        var colMeterX = tableX;
        var colCodeX = colMeterX + colMeterWidth;
        var colZustandX = colCodeX + colCodeWidth;
        var colMpegX = colZustandX + colZustandWidth;
        var colFotoX = colMpegX + colMpegWidth;
        var colStufeX = colFotoX + colFotoWidth;

        var headerY = marginTop + 11;
        var headerLineY = marginTop + headerHeight + 2;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'>");
        sb.Append("<rect width='100%' height='100%' fill='#FFFFFF'/>");

        // --- Defs: Gradients, Patterns, Filters, ClipPaths ---
        sb.Append("<defs>");

        // 3D-Rohr-Gradient (horizontal: hell -> dunkel -> hell)
        sb.Append($"<linearGradient id='pipeGrad' x1='0' y1='0' x2='1' y2='0'>");
        sb.Append($"<stop offset='0%' stop-color='{brand}' stop-opacity='0.3'/>");
        sb.Append($"<stop offset='35%' stop-color='{brand}' stop-opacity='0.85'/>");
        sb.Append($"<stop offset='50%' stop-color='{brand}' stop-opacity='1'/>");
        sb.Append($"<stop offset='65%' stop-color='{brand}' stop-opacity='0.85'/>");
        sb.Append($"<stop offset='100%' stop-color='{brand}' stop-opacity='0.3'/>");
        sb.Append("</linearGradient>");

        // Schaden-Schraffur-Pattern (diagonale rote Linien)
        sb.Append("<pattern id='dmgHatch' patternUnits='userSpaceOnUse' width='6' height='6' patternTransform='rotate(45)'>");
        sb.Append("<line x1='0' y1='0' x2='0' y2='6' stroke='#D64541' stroke-width='2'/>");
        sb.Append("</pattern>");

        // Drop-Shadow fuer Schachtknoten
        sb.Append("<filter id='nodeShadow' x='-30%' y='-30%' width='160%' height='160%'>");
        sb.Append("<feDropShadow dx='1' dy='1' stdDeviation='1.5' flood-color='#00000033'/>");
        sb.Append("</filter>");

        // Boden-Schraffur (horizontale Linien fuer Erdreich)
        sb.Append("<pattern id='groundHatch' patternUnits='userSpaceOnUse' width='4' height='4'>");
        sb.Append("<line x1='0' y1='2' x2='4' y2='2' stroke='#8B7355' stroke-width='0.7'/>");
        sb.Append("</pattern>");

        // Blauer Wasser-Gradient fuer Fliessrichtung
        sb.Append("<linearGradient id='flowGrad' x1='0' y1='0' x2='0' y2='1'>");
        sb.Append("<stop offset='0%' stop-color='#2196F3' stop-opacity='0.9'/>");
        sb.Append("<stop offset='100%' stop-color='#1565C0' stop-opacity='1'/>");
        sb.Append("</linearGradient>");

        // Glow-Filter fuer Fliessrichtungspfeil
        sb.Append("<filter id='flowGlow' x='-50%' y='-50%' width='200%' height='200%'>");
        sb.Append("<feGaussianBlur in='SourceAlpha' stdDeviation='2' result='blur'/>");
        sb.Append("<feFlood flood-color='#2196F3' flood-opacity='0.3'/>");
        sb.Append("<feComposite in2='blur' operator='in'/>");
        sb.Append("<feMerge><feMergeNode/><feMergeNode in='SourceGraphic'/></feMerge>");
        sb.Append("</filter>");

        // ClipPath-Definitionen fuer jede Spalte
        sb.Append($"<clipPath id='clipMeter'><rect x='{Svg(colMeterX)}' y='0' width='{Svg(colMeterWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipCode'><rect x='{Svg(colCodeX)}' y='0' width='{Svg(colCodeWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipZustand'><rect x='{Svg(colZustandX)}' y='0' width='{Svg(colZustandWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipMpeg'><rect x='{Svg(colMpegX)}' y='0' width='{Svg(colMpegWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipFoto'><rect x='{Svg(colFotoX)}' y='0' width='{Svg(colFotoWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append($"<clipPath id='clipStufe'><rect x='{Svg(colStufeX)}' y='0' width='{Svg(colStufeWidth - 2)}' height='{height}'/></clipPath>");
        sb.Append("</defs>");

        // --- Card-Style Spaltenheader ---
        var hdrBgY = marginTop - 2;
        var hdrBgH = headerHeight + 4;
        sb.Append($"<rect x='{Svg(tableX - 4)}' y='{Svg(hdrBgY)}' width='{Svg(tableWidth + 8)}' height='{Svg(hdrBgH)}' rx='4' ry='4' fill='#FFFFFF' stroke='#D1D5DB' stroke-width='0.6'/>");

        sb.Append($"<text x='{Svg(colMeterX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>m+</text>");
        sb.Append($"<text x='{Svg(colCodeX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>OP Kürzel</text>");
        sb.Append($"<text x='{Svg(colZustandX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Zustand</text>");
        sb.Append($"<text x='{Svg(colMpegX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>MPEG</text>");
        sb.Append($"<text x='{Svg(colFotoX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Foto</text>");
        sb.Append($"<text x='{Svg(colStufeX)}' y='{Svg(headerY)}' font-size='11' font-weight='bold' fill='#1F2937' font-family='sans-serif'>Stufe</text>");

        // Vertikale Spaltentrennlinien
        foreach (var cx in new[] { colCodeX, colZustandX, colMpegX, colFotoX, colStufeX })
            sb.Append($"<line x1='{Svg(cx - 3)}' y1='{Svg(hdrBgY + 3)}' x2='{Svg(cx - 3)}' y2='{Svg(hdrBgY + hdrBgH - 3)}' stroke='#D1D5DB' stroke-width='0.5'/>");

        sb.Append($"<line x1='{Svg(tableX - 4)}' y1='{Svg(headerLineY)}' x2='{Svg(width - rightMargin + 4)}' y2='{Svg(headerLineY)}' stroke='#D1D5DB' stroke-width='0.8'/>");

        // --- Alternating tick background stripes ---
        var tickStep = ChooseTickStep(length);
        var ticks = BuildTicks(length, tickStep);
        for (var ti = 0; ti < ticks.Count - 1; ti++)
        {
            if (ti % 2 != 0) continue;
            var yStart = MapToLine(ticks[ti], length, top, bottom);
            var yEnd = MapToLine(ticks[ti + 1], length, top, bottom);
            sb.Append($"<rect x='{Svg(lineX - pipeHalf - 10)}' y='{Svg(yStart)}' width='{Svg(pipeWidth + 20)}' height='{Svg(yEnd - yStart)}' fill='#F5F5F5' rx='2'/>");
        }

        // --- Tick-Markierungen (Messband-Stil) ---
        foreach (var meter in ticks)
        {
            var y = MapToLine(meter, length, top, bottom);
            var isMainTick = Math.Abs(meter % (tickStep * 2)) < 0.001 || meter == 0 || Math.Abs(meter - length) < 0.001;
            var tickLen = isMainTick ? 8d : 5d;
            var strokeW = isMainTick ? "1.2" : "0.8";
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - tickLen)}' y1='{Svg(y)}' x2='{Svg(lineX - pipeHalf)}' y2='{Svg(y)}' stroke='#4A5568' stroke-width='{strokeW}'/>");
            sb.Append($"<text x='{Svg(lineX - pipeHalf - tickLen - 3)}' y='{Svg(y + 3)}' font-size='{(isMainTick ? "11" : "10")}' text-anchor='end' fill='#1F2937' font-family='sans-serif'>{meter:0.00}</text>");
        }

        // --- 3D-Rohr (Gradient-Rechteck statt einfache Linie) ---
        sb.Append($"<rect x='{Svg(lineX - pipeHalf)}' y='{Svg(top)}' width='{Svg(pipeWidth)}' height='{Svg(bottom - top)}' fill='url(#pipeGrad)' rx='3'/>");
        // Rohrwand-Randlinien
        sb.Append($"<line x1='{Svg(lineX - pipeHalf)}' y1='{Svg(top)}' x2='{Svg(lineX - pipeHalf)}' y2='{Svg(bottom)}' stroke='{brand}' stroke-width='0.8' opacity='0.6'/>");
        sb.Append($"<line x1='{Svg(lineX + pipeHalf)}' y1='{Svg(top)}' x2='{Svg(lineX + pipeHalf)}' y2='{Svg(bottom)}' stroke='{brand}' stroke-width='0.8' opacity='0.6'/>");

        // --- Bodenlinien an Schachtpositionen (Erdreich-Darstellung) ---
        var hasAbort = entries.Any(e => IsAbortCode(e));
        var groundW = 30d;
        sb.Append($"<rect x='{Svg(lineX - groundW)}' y='{Svg(top - 2)}' width='{Svg(groundW * 2)}' height='4' fill='url(#groundHatch)'/>");

        if (!hasAbort)
            sb.Append($"<rect x='{Svg(lineX - groundW)}' y='{Svg(bottom - 2)}' width='{Svg(groundW * 2)}' height='4' fill='url(#groundHatch)'/>");

        // --- Schachtknoten: Kreis sitzt UEBER/UNTER dem Rohranfang/-ende ---
        // Rohranfang (top) und Rohrende (bottom) sind der Uebergang Schacht->Haltung.
        // Der Schachtdeckel-Kreis sitzt daher oberhalb bzw. unterhalb des Rohres.
        var nodeR = 11d;
        var topNodeCY = top - nodeR;      // Oberer Schacht: Kreismitte oberhalb Rohranfang
        var bottomNodeCY = bottom + nodeR; // Unterer Schacht: Kreismitte unterhalb Rohrende

        // --- Oberer Schachtknoten: Realistischer Schachtdeckel ---
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(topNodeCY)}' r='{Svg(nodeR)}' fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#nodeShadow)'/>");
        sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(topNodeCY)}' r='{Svg(nodeR * 0.65)}' fill='none' stroke='{brand}' stroke-width='1.2'/>");
        // Radiale Linien (Schachtdeckel-Muster)
        for (var angle = 0; angle < 360; angle += 45)
        {
            var rad = angle * Math.PI / 180.0;
            var x2 = lineX + Math.Cos(rad) * nodeR * 0.9;
            var y2 = topNodeCY + Math.Sin(rad) * nodeR * 0.9;
            sb.Append($"<line x1='{Svg(lineX)}' y1='{Svg(topNodeCY)}' x2='{Svg(x2)}' y2='{Svg(y2)}' stroke='{brand}' stroke-width='0.8' opacity='0.5'/>");
        }

        // --- Unterer Schachtknoten: Nur anzeigen wenn kein Abbruch ---
        if (!hasAbort)
        {
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='{Svg(nodeR)}' fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#nodeShadow)'/>");
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='{Svg(nodeR * 0.65)}' fill='none' stroke='{brand}' stroke-width='1.2'/>");
            sb.Append($"<circle cx='{Svg(lineX)}' cy='{Svg(bottomNodeCY)}' r='3' fill='{brand}'/>");
        }

        // --- Schacht-Beschriftungen ---
        if (!string.IsNullOrWhiteSpace(startNode))
        {
            var startLabelY = Math.Max(8, topNodeCY - nodeR - 4);
            sb.Append($"<text x='{Svg(lineX)}' y='{Svg(startLabelY)}' font-size='13' font-weight='600' text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(startNode)}</text>");
        }
        if (!hasAbort && !string.IsNullOrWhiteSpace(endNode))
        {
            var endLabelY = Math.Min(height - 2, bottomNodeCY + nodeR + 12);
            sb.Append($"<text x='{Svg(lineX)}' y='{Svg(endLabelY)}' font-size='13' font-weight='600' text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(endNode)}</text>");
        }

        // --- Fliessrichtung (Blauer Pfeil mit Wellen) ---
        if (flowDown.HasValue)
        {
            var flowColor = "#2196F3";
            var flowColorDark = "#1565C0";
            var arrowX = lineX; // Pfeil zentriert auf dem Rohr
            var arrowY = flowDown.Value
                ? top + (bottom - top) * 0.30
                : top + (bottom - top) * 0.70;
            var aW = 10d; // Pfeilbreite (Halbbreite)
            var aH = 14d; // Pfeilhoehe

            // Grosser blauer Pfeil mit Glow-Effekt
            if (flowDown.Value)
            {
                // Pfeil nach unten
                sb.Append($"<polygon points='{Svg(arrowX - aW)},{Svg(arrowY - aH / 2)} {Svg(arrowX + aW)},{Svg(arrowY - aH / 2)} {Svg(arrowX)},{Svg(arrowY + aH / 2)}' " +
                          $"fill='url(#flowGrad)' stroke='white' stroke-width='1.5' filter='url(#flowGlow)'/>");
            }
            else
            {
                // Pfeil nach oben
                sb.Append($"<polygon points='{Svg(arrowX - aW)},{Svg(arrowY + aH / 2)} {Svg(arrowX + aW)},{Svg(arrowY + aH / 2)} {Svg(arrowX)},{Svg(arrowY - aH / 2)}' " +
                          $"fill='url(#flowGrad)' stroke='white' stroke-width='1.5' filter='url(#flowGlow)'/>");
            }

            // Wellenlinien (3 Wellen) links neben dem Rohr
            var waveX = lineX - pipeHalf - 10; // Links neben dem Rohr
            var waveCenterY = (top + bottom) / 2.0;
            var waveLen = 40d; // Laenge der Wellenlinien
            var waveAmp = 2.5; // Amplitude der Wellen
            var waveSpacing = 6d; // Abstand zwischen Wellenlinien

            for (var wi = -1; wi <= 1; wi++)
            {
                var wy = waveCenterY + wi * waveSpacing;
                var waveStartY = wy - waveLen / 2;
                // SVG-Pfad fuer Sinuswelle (vertikal, da Rohr vertikal)
                var wavePath = new StringBuilder();
                wavePath.Append($"M {Svg(waveX)} {Svg(waveStartY)}");
                var segments = 8;
                var segLen = waveLen / segments;
                for (var si = 0; si < segments; si++)
                {
                    var cy1 = waveStartY + si * segLen + segLen * 0.33;
                    var cy2 = waveStartY + si * segLen + segLen * 0.66;
                    var ey = waveStartY + (si + 1) * segLen;
                    var dx = (si % 2 == 0) ? waveAmp : -waveAmp;
                    wavePath.Append($" C {Svg(waveX + dx)} {Svg(cy1)}, {Svg(waveX + dx)} {Svg(cy2)}, {Svg(waveX)} {Svg(ey)}");
                }
                sb.Append($"<path d='{wavePath}' fill='none' stroke='{flowColor}' stroke-width='1.2' opacity='0.6'/>");
            }

            // Kleiner Richtungspfeil am Ende der Wellen
            var waveArrowY = flowDown.Value ? waveCenterY + waveLen / 2 + 4 : waveCenterY - waveLen / 2 - 4;
            var waTip = flowDown.Value ? waveArrowY + 5 : waveArrowY - 5;
            sb.Append($"<polygon points='{Svg(waveX - 3)},{Svg(waveArrowY)} {Svg(waveX + 3)},{Svg(waveArrowY)} {Svg(waveX)},{Svg(waTip)}' " +
                      $"fill='{flowColor}' opacity='0.7'/>");

            // Rotierter Label-Text
            var midY = (top + bottom) / 2.0;
            var flowLabel = flowDown.Value ? "↓ Fliessrichtung" : "↑ Fliessrichtung";
            var rotation = flowDown.Value ? 90 : -90;
            sb.Append($"<text x='{Svg(waveX - 10)}' y='{Svg(midY)}' font-size='9' fill='{flowColorDark}' font-weight='600' text-anchor='middle' font-family='sans-serif' " +
                      $"transform='rotate({rotation} {Svg(waveX - 10)} {Svg(midY)})'>{EscapeSvgText(flowLabel)}</text>");
        }

        // --- Streckenschaeden (schraffierte Rohr-Abschnitte) ---
        foreach (var entry in entries)
        {
            if (!entry.IsStreckenschaden || entry.MeterStart is null || entry.MeterEnd is null)
                continue;

            var y1 = MapToLine(entry.MeterStart.Value, length, top, bottom);
            var y2 = MapToLine(entry.MeterEnd.Value, length, top, bottom);
            if (y2 < y1)
                (y1, y2) = (y2, y1);

            var segH = Math.Max(2, y2 - y1);
            // Schraffierter Bereich ueber dem Rohr
            sb.Append($"<rect x='{Svg(lineX - pipeHalf - 1)}' y='{Svg(y1)}' width='{Svg(pipeWidth + 2)}' height='{Svg(segH)}' fill='url(#dmgHatch)' opacity='0.7' rx='2'/>");
            // Rote Randlinien
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - 1)}' y1='{Svg(y1)}' x2='{Svg(lineX + pipeHalf + 1)}' y2='{Svg(y1)}' stroke='#D64541' stroke-width='1.5'/>");
            sb.Append($"<line x1='{Svg(lineX - pipeHalf - 1)}' y1='{Svg(y1 + segH)}' x2='{Svg(lineX + pipeHalf + 1)}' y2='{Svg(y1 + segH)}' stroke='#D64541' stroke-width='1.5'/>");
        }

        // --- Punktschaeden (schadenstypische Symbole) ---
        foreach (var entry in entries)
        {
            if (entry.IsStreckenschaden)
                continue;
            if (IsAbortCode(entry) || IsLateralConnection(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var y = MapToLine(pos.Value, length, top, bottom);
            var category = ClassifyDamageSymbol(entry);
            var symColor = GetDamageSymbolColor(category, brand);
            RenderDamageSymbol(sb, lineX, y, category, symColor);
        }

        // --- Abbruch-Symbol (zwei rote schraege Parallelstriche) ---
        foreach (var entry in entries)
        {
            if (!IsAbortCode(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var ay = MapToLine(pos.Value, length, top, bottom);
            var abortLen = 12d; // Laenge der Striche
            var abortGap = 4d;  // Abstand zwischen den beiden Parallelstrichen
            var abortStroke = 2.5;
            // Erster Strich: schraeg von links-oben nach rechts-unten
            sb.Append($"<line x1='{Svg(lineX - abortLen / 2 - abortGap / 2)}' y1='{Svg(ay - abortLen / 2)}' " +
                      $"x2='{Svg(lineX + abortLen / 2 - abortGap / 2)}' y2='{Svg(ay + abortLen / 2)}' " +
                      $"stroke='#D64541' stroke-width='{Svg(abortStroke)}' stroke-linecap='round'/>");
            // Zweiter Strich: parallel verschoben
            sb.Append($"<line x1='{Svg(lineX - abortLen / 2 + abortGap / 2)}' y1='{Svg(ay - abortLen / 2)}' " +
                      $"x2='{Svg(lineX + abortLen / 2 + abortGap / 2)}' y2='{Svg(ay + abortLen / 2)}' " +
                      $"stroke='#D64541' stroke-width='{Svg(abortStroke)}' stroke-linecap='round'/>");
        }

        // --- Seitenanschluesse (Laterale Rohrstutzen nach Uhrzeitposition) ---
        foreach (var entry in entries)
        {
            if (!IsLateralConnection(entry))
                continue;

            var pos = entry.MeterStart ?? entry.MeterEnd;
            if (pos is null)
                continue;

            var connY = MapToLine(pos.Value, length, top, bottom);
            var clockHour = ExtractClockHour(entry);
            if (clockHour is null)
            {
                // Kein Uhrzeitwert: Standardmaessig nach rechts (3 Uhr)
                clockHour = 3;
            }

            // Winkel berechnen: 12 Uhr = 0 Grad (nach oben), Uhrzeigersinn
            // In der Grafik: 9 Uhr = links, 3 Uhr = rechts
            // Mapping: 3h=rechts(0°), 6h=unten(90°), 9h=links(180°), 12h=oben(270°)
            var angleDeg = (clockHour.Value - 3) * 30.0; // 30° pro Stunde, 3 Uhr = 0°
            var angleRad = angleDeg * Math.PI / 180.0;
            var stubLen = 22d; // Laenge des Rohrstutzens
            var stubEndX = lineX + Math.Cos(angleRad) * (pipeHalf + stubLen);
            var stubEndY = connY + Math.Sin(angleRad) * (pipeHalf + stubLen);
            var stubStartX = lineX + Math.Cos(angleRad) * pipeHalf;
            var stubStartY = connY + Math.Sin(angleRad) * pipeHalf;

            // Rohrstutzen-Linie
            sb.Append($"<line x1='{Svg(stubStartX)}' y1='{Svg(stubStartY)}' x2='{Svg(stubEndX)}' y2='{Svg(stubEndY)}' " +
                      $"stroke='#6B7280' stroke-width='3' stroke-linecap='round'/>");
            // Anschluss-Kreis am Ende
            sb.Append($"<circle cx='{Svg(stubEndX)}' cy='{Svg(stubEndY)}' r='3.5' fill='#6B7280' stroke='white' stroke-width='1'/>");
            // Uhrzeitlabel
            var labelOffsetX = Math.Cos(angleRad) * 8;
            var labelOffsetY = Math.Sin(angleRad) * 8;
            var anchor = clockHour.Value >= 7 && clockHour.Value <= 11 ? "end" : "start";
            if (clockHour.Value == 12 || clockHour.Value == 6) anchor = "middle";
            sb.Append($"<text x='{Svg(stubEndX + labelOffsetX)}' y='{Svg(stubEndY + labelOffsetY + 3)}' " +
                      $"font-size='8' fill='#4B5563' text-anchor='{anchor}' font-family='sans-serif'>" +
                      $"{clockHour.Value}h</text>");
        }

        // --- Beobachtungs-Labels ---
        var labels = BuildHaltungsgrafikLabels(entries, length, top, bottom, photoNumbers, brand);
        LayoutHaltungsgrafikLabels(labels, top, bottom);

        // --- Bezugslinien (Verbindung Beobachtung auf Leitung -> Label-Zeile) ---
        var refStartX = lineX + pipeHalf + 2;
        var refEndX = colMeterX - 4;
        foreach (var label in labels)
        {
            // Kleiner Punkt am Rohr (Abgangspunkt)
            sb.Append($"<circle cx='{Svg(refStartX)}' cy='{Svg(label.TargetY)}' r='1.5' fill='{label.LineColor}' opacity='0.5'/>");
            // Verbindungslinie vom Rohr zur Label-Zeile
            sb.Append($"<line x1='{Svg(refStartX)}' y1='{Svg(label.TargetY)}' x2='{Svg(refEndX)}' y2='{Svg(label.LabelY)}' " +
                      $"stroke='{label.LineColor}' stroke-width='0.6' opacity='0.35' stroke-dasharray='2,1.5'/>");
            // Kleiner Punkt am Label-Ende
            sb.Append($"<circle cx='{Svg(refEndX)}' cy='{Svg(label.LabelY)}' r='1' fill='{label.LineColor}' opacity='0.4'/>");
        }

        foreach (var label in labels)
        {
            var labelY = label.LabelY;

            var textColor = "#111827";
            var fontSize = Svg(label.FontSize);
            sb.Append($"<text clip-path='url(#clipMeter)' x='{Svg(colMeterX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.MeterText)}</text>");
            sb.Append($"<text clip-path='url(#clipCode)' x='{Svg(colCodeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' font-weight='600' text-anchor='start' fill='{brand}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.CodeText)}</text>");
            sb.Append($"<text clip-path='url(#clipZustand)' x='{Svg(colZustandX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.ZustandText)}</text>");
            sb.Append($"<text clip-path='url(#clipMpeg)' x='{Svg(colMpegX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='#4B5563' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.MpegText)}</text>");
            sb.Append($"<text clip-path='url(#clipFoto)' x='{Svg(colFotoX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='#4B5563' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.FotoText)}</text>");
            sb.Append($"<text clip-path='url(#clipStufe)' x='{Svg(colStufeX)}' y='{Svg(labelY + 3)}' font-size='{fontSize}' text-anchor='start' fill='{textColor}' font-family='sans-serif'>" +
                      $"{EscapeSvgText(label.StufeText)}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Prüft ob ein Protokolleintrag einen Inspektions-Abbruch darstellt (BDC-Codes).</summary>
    private static bool IsAbortCode(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BDC* = Abbruch der Inspektion (Hindernis, hoher Wasserstand, Versagen der Ausruestung, etc.)
        return code.StartsWith("BDC", StringComparison.Ordinal);
    }

    /// <summary>Prüft ob ein Protokolleintrag ein Seitenanschluss (lateral connection) ist.</summary>
    private static bool IsLateralConnection(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        // BAG* = Anschluss einragend, BAH* = Anschluss falsch/beschaedigt etc.
        // BCA* = Bestandsaufnahme Anschluss (Formstueck, Sattelanschluss)
        if (code.StartsWith("BAG", StringComparison.Ordinal) ||
            code.StartsWith("BAH", StringComparison.Ordinal) ||
            code.StartsWith("BCAA", StringComparison.Ordinal) ||
            code.StartsWith("BCAB", StringComparison.Ordinal))
            return true;

        // Fallback: Beschreibung enthält "Anschluss" oder "Seiteneinlauf"
        var desc = entry.Beschreibung ?? entry.CodeMeta?.Notes ?? "";
        if (desc.Contains("Anschluss", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Seiteneinlauf", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Extrahiert die Uhrzeitposition (1-12) eines Protokolleintrags.</summary>
    private static int? ExtractClockHour(ProtocolEntry entry)
    {
        var parameters = entry.CodeMeta?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return null;

        // Prioritaet: vsa.uhr.von > ClockPos1
        var raw = GetParam(parameters, "vsa.uhr.von")
               ?? GetParam(parameters, "ClockPos1")
               ?? GetParam(parameters, "Quantifizierung1");

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Versuche die Uhrzeit zu parsen (z.B. "3", "3 Uhr", "03:00", "9")
        var cleaned = Regex.Match(raw.Trim(), @"(\d{1,2})");
        if (cleaned.Success && int.TryParse(cleaned.Groups[1].Value, out var hour) && hour >= 1 && hour <= 12)
            return hour;

        return null;
    }
}
