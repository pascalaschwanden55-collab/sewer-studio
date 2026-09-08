using System.Globalization;
using System.Text;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;
using static AuswertungPro.Next.Application.Reports.ProtocolTextHelpers;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>Ein angeschlossener Rohrstummel (Zulauf oder Ablauf) mit Haltungsname und DN.</summary>
public sealed record SchachtgrafikStummel(string Name, string? Dn);

/// <summary>
/// Ein Schaden, wie ihn <see cref="SchachtgrafikSvgBuilder"/> braucht: Zone, Symbolkategorie
/// und -farbe (aus <see cref="DamageSymbolClassifier"/>) sowie der fertige Hinweistext fuer
/// die Hinweisflaeche.
/// </summary>
public sealed record SchachtgrafikSchadenEintrag(SchachtZone Zone, string Category, string Color, string Tooltip);

/// <summary>
/// Eine anklickbare Hinweisflaeche ueber einem Schadenssymbol der Schachtgrafik. Koordinaten
/// sind SVG-Koordinaten der Grafik (siehe <see cref="SchachtgrafikSvgBuilder.Width"/>/
/// <see cref="SchachtgrafikSvgBuilder.Height"/>).
/// </summary>
public sealed record SchachtgrafikMarke(double X, double Y, double Breite, double Hoehe, string Tooltip);

/// <summary>
/// Generiert den SVG-String fuer die Schachtgrafik: ein senkrechter Schnitt durch den Schacht
/// (WinCan-Art) mit Deckel, den Zonen Konus/Schachtwand/Sohle, Tiefen-Masslinie, Innenmassen,
/// angeschlossenen Haltungen als Rohrstummel links (Zulauf) und rechts (Ablauf), Fliesspfeil
/// und Schadenssymbolen je Zone.
///
/// Die Grafik ist bewusst schmal und hoch (wie die Rohrsaeule der Haltung): Sie steht in einer
/// schmalen Spalte der Uebersicht, nicht auf einem A4-Blatt.
///
/// Schreibt nur Elemente/Attribute der bestehenden SVG-Teilmenge
/// (<c>SvgTeilmengeZeichner</c>) und nur Farben aus <c>SvgFarbZuordnung</c> — dieselbe Palette
/// wie <see cref="HaltungsgrafikSvgBuilder"/>, damit die Zuordnung ohne neue Eintraege greift.
///
/// Keine erfundenen Werte: Fehlt die Tiefe, entsteht keine Masslinie, sondern ein Hinweistext
/// unter der Grafik. Fehlt eines der beiden Innenmasse, steht dort ein Gedankenstrich.
/// </summary>
public static class SchachtgrafikSvgBuilder
{
    public const int Width = 230;
    public const int Height = 400;

    private const double CenterX = 92d;
    private const double GroundY = 26d;
    private const double DeckelRx = 26d;
    private const double DeckelRy = 7d;
    private const double KonusTopHalfWidth = 24d;
    private const double KonusBottomHalfWidth = 15d;
    private const double KonusHeight = 50d;
    private const double WandHalfWidth = 15d;
    private const double WandHeight = 170d;
    private const double SohleHeight = 30d;
    private const double StubLength = 16d;
    private const double MassLinieX = Width - 20d;
    private const double SymbolGroesse = 16d;
    private const int MaxStummelJeSeite = 4;
    private const int NameKuerzeZeichen = 8;

    private static readonly double KonusTopY = GroundY + DeckelRy;
    private static readonly double KonusBottomY = KonusTopY + KonusHeight;
    private static readonly double WandTopY = KonusBottomY;
    private static readonly double WandBottomY = WandTopY + WandHeight;
    private static readonly double SohleTopY = WandBottomY;
    private static readonly double SohleBottomY = SohleTopY + SohleHeight;
    private static readonly double InnenmasseY = SohleBottomY + 20d;
    private static readonly double TiefeHinweisY = InnenmasseY + 18d;

    /// <summary>
    /// Baut die Schachtgrafik. <paramref name="tiefeMeter"/> ist <c>null</c>, wenn keine Tiefe
    /// erfasst ist — dann entsteht keine Masslinie, sondern ein Hinweistext. Fehlt eines der
    /// beiden Innenmasse, steht dort ein Gedankenstrich statt eines halben, irrefuehrenden Paars.
    /// </summary>
    public static (string Svg, IReadOnlyList<SchachtgrafikMarke> Marken) Baue(
        string? schachtnummer,
        double? tiefeMeter,
        string? dimension1Mm,
        string? dimension2Mm,
        IReadOnlyList<SchachtgrafikStummel> zulaeufe,
        IReadOnlyList<SchachtgrafikStummel> ablaeufe,
        IReadOnlyList<SchachtgrafikSchadenEintrag> schaeden,
        string brand = "#006E9C")
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{Height}' viewBox='0 0 {Width} {Height}'>");
        sb.Append("<rect width='100%' height='100%' fill='#FFFFFF'/>");

        sb.Append("<defs>");
        sb.Append("<pattern id='groundHatch' patternUnits='userSpaceOnUse' width='4' height='4'>");
        sb.Append("<line x1='0' y1='2' x2='4' y2='2' stroke='#8B7355' stroke-width='0.7'/>");
        sb.Append("</pattern>");
        sb.Append("<filter id='deckelShadow' x='-30%' y='-30%' width='160%' height='160%'>");
        sb.Append("<feDropShadow dx='1' dy='1' stdDeviation='1.5' flood-color='#00000033'/>");
        sb.Append("</filter>");
        sb.Append("<linearGradient id='flowGrad' x1='0' y1='0' x2='0' y2='1'>");
        sb.Append("<stop offset='0%' stop-color='#2196F3' stop-opacity='0.9'/>");
        sb.Append("<stop offset='100%' stop-color='#1565C0' stop-opacity='1'/>");
        sb.Append("</linearGradient>");
        sb.Append("<filter id='flowGlow' x='-50%' y='-50%' width='200%' height='200%'>");
        sb.Append("<feGaussianBlur in='SourceAlpha' stdDeviation='2' result='blur'/>");
        sb.Append("<feFlood flood-color='#2196F3' flood-opacity='0.3'/>");
        sb.Append("<feComposite in2='blur' operator='in'/>");
        sb.Append("<feMerge><feMergeNode/><feMergeNode in='SourceGraphic'/></feMerge>");
        sb.Append("</filter>");
        sb.Append("</defs>");

        // --- Bodenoberflaeche und Deckel ---
        sb.Append($"<rect x='{Svg(CenterX - 40)}' y='{Svg(GroundY - 3)}' width='80' height='6' fill='url(#groundHatch)'/>");
        sb.Append($"<ellipse cx='{Svg(CenterX)}' cy='{Svg(GroundY)}' rx='{Svg(DeckelRx)}' ry='{Svg(DeckelRy)}' " +
                  $"fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#deckelShadow)'/>");
        sb.Append($"<ellipse cx='{Svg(CenterX)}' cy='{Svg(GroundY)}' rx='{Svg(DeckelRx * 0.6)}' ry='{Svg(DeckelRy * 0.6)}' " +
                  $"fill='none' stroke='{brand}' stroke-width='1.2'/>");
        if (!string.IsNullOrWhiteSpace(schachtnummer))
        {
            sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(GroundY - 14)}' font-size='12' font-weight='600' " +
                      $"text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(schachtnummer.Trim())}</text>");
        }

        // --- Konus (Trapez) ---
        sb.Append($"<path d='M {Svg(CenterX - KonusTopHalfWidth)},{Svg(KonusTopY)} " +
                  $"L {Svg(CenterX + KonusTopHalfWidth)},{Svg(KonusTopY)} " +
                  $"L {Svg(CenterX + KonusBottomHalfWidth)},{Svg(KonusBottomY)} " +
                  $"L {Svg(CenterX - KonusBottomHalfWidth)},{Svg(KonusBottomY)} Z' " +
                  $"fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");

        // --- Schachtwand ---
        sb.Append($"<rect x='{Svg(CenterX - WandHalfWidth)}' y='{Svg(WandTopY)}' width='{Svg(WandHalfWidth * 2)}' " +
                  $"height='{Svg(WandHeight)}' fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");

        // --- Sohle mit Durchlaufrinne ---
        sb.Append($"<rect x='{Svg(CenterX - WandHalfWidth)}' y='{Svg(SohleTopY)}' width='{Svg(WandHalfWidth * 2)}' " +
                  $"height='{Svg(SohleHeight)}' fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");
        sb.Append($"<path d='M {Svg(CenterX - WandHalfWidth)},{Svg(SohleTopY + 6)} " +
                  $"Q {Svg(CenterX)},{Svg(SohleBottomY - 3)} {Svg(CenterX + WandHalfWidth)},{Svg(SohleTopY + 6)}' " +
                  $"fill='none' stroke='{brand}' stroke-width='1.4'/>");

        // --- Zonenbeschriftung links ---
        var zonenLabelX = CenterX - WandHalfWidth - StubLength - 6;
        Beschriftung(sb, zonenLabelX, (KonusTopY + KonusBottomY) / 2d, "Konus", "end");
        Beschriftung(sb, zonenLabelX, (WandTopY + WandBottomY) / 2d, "Schachtwand", "end", groesse: 7);
        Beschriftung(sb, zonenLabelX, (SohleTopY + SohleBottomY) / 2d, "Sohle", "end");

        // --- Fliesspfeil (Zulauf links -> Ablauf rechts) ---
        var pfeilY = (WandTopY + WandBottomY) / 2d;
        var pfeilH = 8d;
        var pfeilL = 9d;
        sb.Append($"<polygon points='{Svg(CenterX - pfeilL)},{Svg(pfeilY - pfeilH)} {Svg(CenterX - pfeilL)},{Svg(pfeilY + pfeilH)} " +
                  $"{Svg(CenterX + pfeilL)},{Svg(pfeilY)}' fill='url(#flowGrad)' stroke='white' stroke-width='1' filter='url(#flowGlow)'/>");

        // --- Zu-/Ablauf-Stummel ---
        var marken = new List<SchachtgrafikMarke>();
        Stummelseite(sb, zulaeufe, links: true);
        Stummelseite(sb, ablaeufe, links: false);

        // --- Schaeden je Zone (Anschluss teilt sich die Zeichenflaeche der Schachtwand) ---
        ZeichneSchaeden(sb, marken, schaeden, SchachtZone.Konus, KonusTopY + 10, KonusBottomY - 6);
        ZeichneSchaeden(sb, marken, schaeden, SchachtZone.Schachtwand, WandTopY + 14, WandBottomY - 14, auchZone: SchachtZone.Anschluss);
        ZeichneSchaeden(sb, marken, schaeden, SchachtZone.Sohle, SohleTopY + 6, SohleBottomY - 6);

        // --- Tiefen-Masslinie oder Hinweis ---
        if (tiefeMeter is > 0)
        {
            sb.Append($"<line x1='{Svg(MassLinieX)}' y1='{Svg(GroundY)}' x2='{Svg(MassLinieX)}' y2='{Svg(SohleBottomY)}' " +
                      $"stroke='{brand}' stroke-width='1'/>");
            sb.Append($"<line x1='{Svg(MassLinieX - 4)}' y1='{Svg(GroundY)}' x2='{Svg(MassLinieX + 4)}' y2='{Svg(GroundY)}' stroke='{brand}' stroke-width='1'/>");
            sb.Append($"<line x1='{Svg(MassLinieX - 4)}' y1='{Svg(SohleBottomY)}' x2='{Svg(MassLinieX + 4)}' y2='{Svg(SohleBottomY)}' stroke='{brand}' stroke-width='1'/>");
            var tiefeText = tiefeMeter.Value.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            var textX = MassLinieX + 10;
            var textY = (GroundY + SohleBottomY) / 2d;
            // Senkrecht gedreht: Der Wert braucht so nur die Breite eines Zeichens statt der
            // ganzen Zahl — sonst raegt die Beschriftung ueber den schmalen rechten Rand hinaus.
            sb.Append($"<text x='{Svg(textX)}' y='{Svg(textY)}' font-size='10' text-anchor='middle' fill='#1F2937' " +
                      $"font-family='sans-serif' transform='rotate(-90 {Svg(textX)} {Svg(textY)})'>{EscapeSvgText(tiefeText)}</text>");
        }
        else
        {
            sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(TiefeHinweisY)}' font-size='9' text-anchor='middle' " +
                      $"fill='#6B7280' font-family='sans-serif'>Tiefe nicht erfasst</text>");
        }

        // --- Innenmasse unter der Sohle ---
        var hatMasse = !string.IsNullOrWhiteSpace(dimension1Mm) && !string.IsNullOrWhiteSpace(dimension2Mm);
        var masseText = hatMasse ? $"{dimension1Mm!.Trim()} × {dimension2Mm!.Trim()}" : "–";
        sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(InnenmasseY)}' font-size='11' font-weight='600' text-anchor='middle' " +
                  $"fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(masseText)}</text>");

        sb.Append("</svg>");
        return (sb.ToString(), marken);

        void Stummelseite(StringBuilder ziel, IReadOnlyList<SchachtgrafikStummel> stummel, bool links)
        {
            var sichtbar = stummel.Take(MaxStummelJeSeite).ToList();
            var rest = stummel.Count - sichtbar.Count;
            var slots = sichtbar.Count + (rest > 0 ? 1 : 0);
            var positionen = VerteileY(WandTopY + 14, WandBottomY - 14, slots);

            var wandX = links ? CenterX - WandHalfWidth : CenterX + WandHalfWidth;
            var spitzeX = links ? wandX - StubLength : wandX + StubLength;
            var textX = links ? spitzeX - 3 : spitzeX + 3;
            var anker = links ? "end" : "start";

            for (var i = 0; i < sichtbar.Count; i++)
            {
                var y = positionen[i];
                ziel.Append($"<line x1='{Svg(wandX)}' y1='{Svg(y)}' x2='{Svg(spitzeX)}' y2='{Svg(y)}' " +
                            $"stroke='#6B7280' stroke-width='3' stroke-linecap='round'/>");
                ziel.Append($"<circle cx='{Svg(spitzeX)}' cy='{Svg(y)}' r='3' fill='#6B7280' stroke='white' stroke-width='1'/>");
                var name = KuerzeMitEllipse(sichtbar[i].Name, NameKuerzeZeichen);
                var beschriftung = sichtbar[i].Dn is { } dn ? $"{name} {dn}" : name;
                Beschriftung(ziel, textX, y + 3, beschriftung, anker, groesse: 7, farbe: "#4B5563");
            }

            if (rest > 0)
                Beschriftung(ziel, textX, positionen[^1] + 3, $"+{rest}", anker, groesse: 7, farbe: "#4B5563");
        }
    }

    /// <summary>Zeichnet alle Schaeden einer Zone (und optional einer zweiten, mitgezeichneten Zone) mittig gestapelt.</summary>
    private static void ZeichneSchaeden(
        StringBuilder sb,
        List<SchachtgrafikMarke> marken,
        IReadOnlyList<SchachtgrafikSchadenEintrag> schaeden,
        SchachtZone zone,
        double top,
        double bottom,
        SchachtZone? auchZone = null)
    {
        var treffer = schaeden.Where(s => s.Zone == zone || s.Zone == auchZone).ToList();
        if (treffer.Count == 0)
            return;

        var positionen = VerteileY(top, bottom, treffer.Count);
        for (var i = 0; i < treffer.Count; i++)
        {
            var y = positionen[i];
            DamageSymbolRenderer.RenderDamageSymbol(sb, CenterX, y, treffer[i].Category, treffer[i].Color);
            marken.Add(new SchachtgrafikMarke(
                CenterX - SymbolGroesse / 2d, y - SymbolGroesse / 2d, SymbolGroesse, SymbolGroesse, treffer[i].Tooltip));
        }
    }

    /// <summary>Verteilt <paramref name="anzahl"/> Punkte gleichmaessig im offenen Bereich (top, bottom).</summary>
    private static IReadOnlyList<double> VerteileY(double top, double bottom, int anzahl)
    {
        if (anzahl <= 0)
            return Array.Empty<double>();
        if (anzahl == 1)
            return [(top + bottom) / 2d];

        var schritt = (bottom - top) / (anzahl + 1);
        var ergebnis = new List<double>(anzahl);
        for (var i = 1; i <= anzahl; i++)
            ergebnis.Add(top + schritt * i);
        return ergebnis;
    }

    private static void Beschriftung(
        StringBuilder sb, double x, double y, string text, string anker, int groesse = 8, string farbe = "#6B7280")
    {
        sb.Append($"<text x='{Svg(x)}' y='{Svg(y)}' font-size='{groesse}' text-anchor='{anker}' " +
                  $"fill='{farbe}' font-family='sans-serif'>{EscapeSvgText(text)}</text>");
    }
}
