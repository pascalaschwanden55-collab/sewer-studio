using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Theme;
using Mapsui.Nts;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Schadenspunkte der GEWAEHLTEN Haltung: jeder Protokolleintrag mit Meterstand wird
/// entlang der Linie interpoliert und als Punkt in Severity-Farbe gezeichnet
/// (Overlay-Rampe der zentralen Farbquelle — gesaettigt, karten-lesbar).
/// Bewusst nur eine Haltung auf einmal: Klick zeigt ihre Schaeden, keine Punktwolke.
/// </summary>
public static class KarteSchadenFeatureBuilder
{
    public static IReadOnlyList<GeometryFeature> Build(
        Geometry? haltungsGeometrie,
        IEnumerable<ProtocolEntry>? entries,
        double? sollLaengeMeter)
    {
        if (haltungsGeometrie is not LineString linie || linie.Coordinates.Length < 2 || entries is null)
            return [];

        var punkte = linie.Coordinates.Select(c => (c.X, c.Y)).ToArray();
        var ergebnis = new List<GeometryFeature>();

        foreach (var entry in entries)
        {
            if (entry.IsDeleted || entry.MeterStart is not { } meter)
                continue;

            var position = SchadenPositionInterpolator.Interpoliere(punkte, meter, sollLaengeMeter);
            if (position is null)
                continue;

            var severity = ParseSeverity(entry.CodeMeta?.Severity);
            var farbe = StatusColors.Current.SeverityOverlay(severity);

            var feature = new GeometryFeature { Geometry = new Point(position.Value.X, position.Value.Y) };
            feature["Code"] = entry.Code;
            feature["Meter"] = meter;
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                SymbolScale = 0.38,
                Fill = new Mapsui.Styles.Brush(new Color(farbe.R, farbe.G, farbe.B)),
                Outline = new Pen(new Color(255, 255, 255, 240), 2)
            });
            ergebnis.Add(feature);
        }

        return ergebnis;
    }

    // Severity im Protokoll ist ein freier String ("3", "S3", leer) — tolerant parsen, Mitte als Default.
    private static int ParseSeverity(string? roh)
    {
        if (string.IsNullOrWhiteSpace(roh))
            return 3;

        var ziffern = new string(roh.Where(char.IsDigit).ToArray());
        return int.TryParse(ziffern, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
            ? Math.Clamp(s, 1, 5)
            : 3;
    }
}
