using System.Text;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Rendert schadenstypische SVG-Symbole fuer die Haltungsgrafik.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral).
/// Abhaengigkeiten: nur <see cref="ProtocolPdfValueFormatting.Svg"/> (StringBuilder/Math).
/// </summary>
public static class DamageSymbolRenderer
{
    /// <summary>
    /// Rendert ein schadenstypisches SVG-Symbol zentriert auf (<paramref name="cx"/>, <paramref name="cy"/>).
    /// </summary>
    public static void RenderDamageSymbol(StringBuilder sb, double cx, double cy, string category, string color, double s = 5)
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

            case "incrustation":
            case "deposit": // Geschichtete Linien (Anhaftung / Ablagerung)
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
