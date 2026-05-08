using System;
using System.Text;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// Schadens-Symbol-Block des Protokoll-PDF: VSA-Code -> Symbol-Kategorie ->
// Farbe + SVG-Symbol-Renderer. Aus dem Hauptdatei extrahiert (Slice 1d).
public sealed partial class ProtocolPdfExporter
{
    /// <summary>Klassifiziert einen Schaden nach Symbol-Kategorie anhand des VSA-Codes.</summary>
    private static string ClassifyDamageSymbol(ProtocolEntry entry)
    {
        var code = (entry.Code ?? "").Trim().ToUpperInvariant();
        if (code.StartsWith("BAA", StringComparison.Ordinal)) return "deformation";  // Verformung
        if (code.StartsWith("BAB", StringComparison.Ordinal)) return "crack";        // Risse
        if (code.StartsWith("BAC", StringComparison.Ordinal)) return "break";        // Bruch / Einsturz
        if (code.StartsWith("BAD", StringComparison.Ordinal)) return "break";        // Defektes Mauerwerk
        if (code.StartsWith("BAE", StringComparison.Ordinal)) return "surface";      // Fehlender Moertel
        if (code.StartsWith("BAF", StringComparison.Ordinal)) return "surface";      // Oberflaechenschaden
        if (code.StartsWith("BAI", StringComparison.Ordinal)) return "obstacle";     // Einragendes Dichtungsmaterial
        if (code.StartsWith("BAJ", StringComparison.Ordinal)) return "offset";       // Verschobene Rohrverbindung
        if (code.StartsWith("BAK", StringComparison.Ordinal)) return "surface";      // Innenauskleidung
        if (code.StartsWith("BAL", StringComparison.Ordinal)) return "break";        // Schadhafte Reparatur
        if (code.StartsWith("BBA", StringComparison.Ordinal)) return "deposit";      // Ablagerung
        if (code.StartsWith("BBB", StringComparison.Ordinal)) return "obstacle";     // Verstopfung
        return "default";
    }

    /// <summary>Gibt die harmonisierte Farbe fuer eine Schadens-Kategorie zurueck.</summary>
    private static string GetDamageSymbolColor(string category, string fallback = "#006E9C")
    {
        return category switch
        {
            "crack" or "break"                          => "#D64541", // Rot - strukturell kritisch
            "deformation" or "offset" or "surface"      => "#E67E22", // Orange - Verformung / Oberflaeche
            "leak" or "infiltration" or "exfiltration"   => "#2196F3", // Blau - Wasser
            "roots"                                      => "#27AE60", // Gruen - biologisch
            "deposit"                                    => "#8B6914", // Braun - Ablagerung
            "obstacle"                                   => "#6B7280", // Grau - Hindernis
            _ => fallback
        };
    }

    /// <summary>Rendert ein schadenstypisches SVG-Symbol zentriert auf (cx, cy).</summary>
    private static void RenderDamageSymbol(StringBuilder sb, double cx, double cy, string category, string color, double s = 5)
    {
        // Weisser Hintergrund-Kreis fuer Kontrast auf dem Rohr-Gradient
        sb.Append($"<circle cx='{Svg(cx)}' cy='{Svg(cy)}' r='{Svg(s + 1.5)}' fill='white' opacity='0.85'/>");

        switch (category)
        {
            case "crack": // Blitz-Zickzack (Rissbildung)
                sb.Append($"<path d='M {Svg(cx)},{Svg(cy - s)} L {Svg(cx + s * 0.5)},{Svg(cy - s * 0.15)} " +
                          $"L {Svg(cx - s * 0.5)},{Svg(cy + s * 0.15)} L {Svg(cx)},{Svg(cy + s)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "break": // X-Kreuz (Bruch / Einsturz)
                sb.Append($"<line x1='{Svg(cx - s * 0.7)}' y1='{Svg(cy - s * 0.7)}' x2='{Svg(cx + s * 0.7)}' y2='{Svg(cy + s * 0.7)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx + s * 0.7)}' y1='{Svg(cy - s * 0.7)}' x2='{Svg(cx - s * 0.7)}' y2='{Svg(cy + s * 0.7)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                break;

            case "deformation": // Gequetschte Ellipse (Deformation)
                sb.Append($"<ellipse cx='{Svg(cx)}' cy='{Svg(cy)}' rx='{Svg(s)}' ry='{Svg(s * 0.5)}' " +
                          $"fill='none' stroke='{color}' stroke-width='1.8'/>");
                break;

            case "leak": // Wassertropfen (Undichtheit)
                sb.Append($"<path d='M {Svg(cx)},{Svg(cy - s)} " +
                          $"Q {Svg(cx + s * 0.7)},{Svg(cy + s * 0.2)} {Svg(cx)},{Svg(cy + s)} " +
                          $"Q {Svg(cx - s * 0.7)},{Svg(cy + s * 0.2)} {Svg(cx)},{Svg(cy - s)} Z' " +
                          $"fill='{color}' opacity='0.85'/>");
                break;

            case "offset": // Versatz-Stufe
                sb.Append($"<path d='M {Svg(cx - s)},{Svg(cy - s * 0.5)} L {Svg(cx)},{Svg(cy - s * 0.5)} " +
                          $"L {Svg(cx)},{Svg(cy + s * 0.5)} L {Svg(cx + s)},{Svg(cy + s * 0.5)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "surface": // Wellige Linie (Oberflaechenschaden)
                sb.Append($"<path d='M {Svg(cx - s)},{Svg(cy)} " +
                          $"Q {Svg(cx - s * 0.5)},{Svg(cy - s * 0.6)} {Svg(cx)},{Svg(cy)} " +
                          $"Q {Svg(cx + s * 0.5)},{Svg(cy + s * 0.6)} {Svg(cx + s)},{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' fill='none' stroke-linecap='round'/>");
                break;

            case "obstacle": // Gefuelltes Quadrat (Hindernis / Verstopfung)
                sb.Append($"<rect x='{Svg(cx - s * 0.6)}' y='{Svg(cy - s * 0.6)}' " +
                          $"width='{Svg(s * 1.2)}' height='{Svg(s * 1.2)}' " +
                          $"fill='{color}' rx='1'/>");
                break;

            case "roots": // Y-Gabel (Wurzeleinwuchs)
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy + s)}' x2='{Svg(cx)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy)}' x2='{Svg(cx - s * 0.6)}' y2='{Svg(cy - s)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.6)}' y2='{Svg(cy - s)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                break;

            case "infiltration": // Pfeil nach innen (Wassereintritt)
                sb.Append($"<line x1='{Svg(cx + s)}' y1='{Svg(cy)}' x2='{Svg(cx - s * 0.3)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<path d='M {Svg(cx + s * 0.2)},{Svg(cy - s * 0.4)} L {Svg(cx - s * 0.3)},{Svg(cy)} L {Svg(cx + s * 0.2)},{Svg(cy + s * 0.4)}' " +
                          $"stroke='{color}' stroke-width='1.8' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "exfiltration": // Pfeil nach aussen (Wasseraustritt)
                sb.Append($"<line x1='{Svg(cx - s)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.3)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='2' stroke-linecap='round'/>");
                sb.Append($"<path d='M {Svg(cx - s * 0.2)},{Svg(cy - s * 0.4)} L {Svg(cx + s * 0.3)},{Svg(cy)} L {Svg(cx - s * 0.2)},{Svg(cy + s * 0.4)}' " +
                          $"stroke='{color}' stroke-width='1.8' fill='none' stroke-linecap='round' stroke-linejoin='round'/>");
                break;

            case "deposit": // Geschichtete Linien (Ablagerung)
                sb.Append($"<line x1='{Svg(cx - s * 0.8)}' y1='{Svg(cy)}' x2='{Svg(cx + s * 0.8)}' y2='{Svg(cy)}' " +
                          $"stroke='{color}' stroke-width='1.8' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx - s * 0.5)}' y1='{Svg(cy + s * 0.5)}' x2='{Svg(cx + s * 0.5)}' y2='{Svg(cy + s * 0.5)}' " +
                          $"stroke='{color}' stroke-width='1.5' stroke-linecap='round'/>");
                sb.Append($"<line x1='{Svg(cx - s * 0.3)}' y1='{Svg(cy + s)}' x2='{Svg(cx + s * 0.3)}' y2='{Svg(cy + s)}' " +
                          $"stroke='{color}' stroke-width='1.2' stroke-linecap='round'/>");
                break;

            default: // Diamant (Allgemein / unbekannt)
                sb.Append($"<polygon points='{Svg(cx)},{Svg(cy - s)} {Svg(cx + s)},{Svg(cy)} {Svg(cx)},{Svg(cy + s)} {Svg(cx - s)},{Svg(cy)}' " +
                          $"fill='{color}' stroke='white' stroke-width='1.2'/>");
                break;
        }
    }
}
