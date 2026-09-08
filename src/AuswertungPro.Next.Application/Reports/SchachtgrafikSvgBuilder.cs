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
/// Eine anklickbare Hinweisflaeche ueber einem Schadenssymbol oder einer gekuerzten
/// Zu-/Ablauf-Beschriftung der Schachtgrafik. Koordinaten sind SVG-Koordinaten der Grafik
/// (siehe <see cref="SchachtgrafikSvgBuilder.Width"/>/<see cref="SchachtgrafikSvgBuilder.Height"/>).
/// </summary>
public sealed record SchachtgrafikMarke(double X, double Y, double Breite, double Hoehe, string Tooltip);

/// <summary>
/// Generiert den SVG-String fuer die Schachtgrafik: ein senkrechter Schnitt durch den Schacht
/// (WinCan-Art) mit Deckel, den Zonen Konus/Schachtwand/Sohle, Tiefen-Masslinie, Innenmassen,
/// angeschlossenen Haltungen als Rohrstummel links (Zulauf) und rechts (Ablauf), Fliesspfeil
/// und Schadenssymbolen je Zone.
///
/// Die Zeichenflaeche ist schmal und hoch, aber der Schachtkoerper selbst ist BREIT (Durchmesser
/// mindestens ein Drittel der Zeichenbreite) — anders als die duenne Rohrsaeule der Haltung ist
/// ein Schacht kein Rohr, sondern ein Bauwerk mit realer Ausdehnung.
///
/// Fix-Runde 1 (Controller-Sichtprobe): Die erste Fassung (230 x 400) war zu klein fuer die
/// Panelbreite, die Zonenbeschriftung ueberlagerte sich mit der weit aussen stehenden
/// Ablauf-Beschriftung und der Masslinie. Die Masslinie steht deshalb jetzt AUSSERHALB der
/// Ablauf-Beschriftung (Reihenfolge von der Wand nach aussen: Stummel, Beschriftung, Masslinie),
/// die Zeichenflaeche ist auf 320 x 520 gewachsen (gleiches Seitenverhaeltnis wie zuvor
/// vorgeschlagen, mit Reserve fuer alle drei rechten Elemente ohne Ueberlappung).
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
    public const int Width = 320;
    public const int Height = 520;

    /// <summary>
    /// Durchmesser des gezeichneten Schachtkoerpers (Schachtwand/Sohle) in SVG-Einheiten.
    /// Controller-Ruling (Fix-Runde 1): mindestens ein Drittel von <see cref="Width"/> — ein
    /// Schacht ist kein Rohr, sondern ein Bauwerk mit realer Ausdehnung.
    /// </summary>
    public const double SchachtkoerperDurchmesser = WandHalfWidth * 2d;

    private const double CenterX = 160d;
    private const double GroundY = 40d;
    private const double DeckelRx = 58d;
    private const double DeckelRy = 15d;
    private const double KonusTopHalfWidth = 64d;
    private const double KonusBottomHalfWidth = 54d;
    private const double KonusHeight = 84d;

    /// <summary>Halbe Breite des Schachtkoerpers: Durchmesser 108 SVG-Einheiten, rund 34 % der Zeichenbreite.</summary>
    private const double WandHalfWidth = 54d;
    private const double WandHeight = 216d;
    private const double SohleHeight = 60d;

    /// <summary>Laenge der Zu-/Ablauf-Stummel — kurz gehalten, damit auf beiden Seiten Platz fuer die Beschriftung bleibt.</summary>
    private const double StubLength = 16d;
    private const int MaxStummelJeSeite = 2;

    /// <summary>Kuerzungslaenge der GANZEN Beschriftung ("Name DN200"), nicht nur des Namens.</summary>
    private const int LabelKuerzeZeichen = 8;

    private const double SymbolGroesse = 18d;

    // Mindestgroessen nach Skalierung (Controller-Ruling, Fix-Runde 1): Striche >= 3 px, Schrift
    // >= 9 px. Die SVG-Werte liegen bewusst darueber, weil die Grafik in der Standardspalte auf
    // rund 94 % ihrer nativen Groesse herunterskaliert wird (breitengebunden, siehe
    // SchachtgrafikControlIsolatedSmokeTests) und trotzdem Reserve behalten muss.
    private const double StrichStark = 4d;
    private const double MasslinieStrich = 4d;
    private const int SchriftKlein = 11;

    private static readonly double KonusTopY = GroundY + DeckelRy;
    private static readonly double KonusBottomY = KonusTopY + KonusHeight;
    private static readonly double WandTopY = KonusBottomY;
    private static readonly double WandBottomY = WandTopY + WandHeight;
    private static readonly double SohleTopY = WandBottomY;
    private static readonly double SohleBottomY = SohleTopY + SohleHeight;
    private static readonly double InnenmasseY = SohleBottomY + 28d;
    private static readonly double TiefeHinweisY = InnenmasseY + 26d;

    // Rechte Seite (Ablauf): von der Wand nach aussen Stummel, dann Beschriftung, ERST DANACH die
    // Masslinie — so kreuzen sich Ablauf-Beschriftung und Masslinie nie.
    private static readonly double WallRightX = CenterX + WandHalfWidth;
    private static readonly double AblaufStubTipX = WallRightX + StubLength;
    private static readonly double AblaufLabelX = AblaufStubTipX + 4d;
    private static readonly double MassLinieX = AblaufLabelX + 56d;
    private static readonly double TiefeTextX = MassLinieX + 8d;

    // Linke Seite (Zulauf): Zonenbeschriftung sitzt direkt an der Stummelspitze.
    private static readonly double WallLeftX = CenterX - WandHalfWidth;
    private static readonly double ZulaufStubTipX = WallLeftX - StubLength;
    private static readonly double ZonenLabelX = ZulaufStubTipX;

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
        sb.Append($"<rect x='{Svg(CenterX - 85)}' y='{Svg(GroundY - 3)}' width='170' height='6' fill='url(#groundHatch)'/>");
        sb.Append($"<ellipse cx='{Svg(CenterX)}' cy='{Svg(GroundY)}' rx='{Svg(DeckelRx)}' ry='{Svg(DeckelRy)}' " +
                  $"fill='#F5F5F5' stroke='#4A5568' stroke-width='1.8' filter='url(#deckelShadow)'/>");
        sb.Append($"<ellipse cx='{Svg(CenterX)}' cy='{Svg(GroundY)}' rx='{Svg(DeckelRx * 0.6)}' ry='{Svg(DeckelRy * 0.6)}' " +
                  $"fill='none' stroke='{brand}' stroke-width='1.2'/>");
        if (!string.IsNullOrWhiteSpace(schachtnummer))
        {
            sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(GroundY - 20)}' font-size='13' font-weight='600' " +
                      $"text-anchor='middle' fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(schachtnummer.Trim())}</text>");
        }

        // --- Konus (Trapez) ---
        sb.Append($"<path d='M {Svg(CenterX - KonusTopHalfWidth)},{Svg(KonusTopY)} " +
                  $"L {Svg(CenterX + KonusTopHalfWidth)},{Svg(KonusTopY)} " +
                  $"L {Svg(CenterX + KonusBottomHalfWidth)},{Svg(KonusBottomY)} " +
                  $"L {Svg(CenterX - KonusBottomHalfWidth)},{Svg(KonusBottomY)} Z' " +
                  $"fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");

        // --- Schachtwand ---
        sb.Append($"<rect x='{Svg(WallLeftX)}' y='{Svg(WandTopY)}' width='{Svg(WandHalfWidth * 2)}' " +
                  $"height='{Svg(WandHeight)}' fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");

        // --- Sohle mit Durchlaufrinne ---
        sb.Append($"<rect x='{Svg(WallLeftX)}' y='{Svg(SohleTopY)}' width='{Svg(WandHalfWidth * 2)}' " +
                  $"height='{Svg(SohleHeight)}' fill='#F5F5F5' stroke='#D1D5DB' stroke-width='1'/>");
        sb.Append($"<path d='M {Svg(WallLeftX)},{Svg(SohleBottomY - 12)} " +
                  $"Q {Svg(CenterX)},{Svg(SohleBottomY - 2)} {Svg(WallRightX)},{Svg(SohleBottomY - 12)}' " +
                  $"fill='none' stroke='{brand}' stroke-width='1.4'/>");

        // --- Zonenbeschriftung links, je nahe dem oberen Rand der Zone (nicht mittig — Platz
        //     fuer Stummel und Symbole darunter). ---
        Beschriftung(sb, ZonenLabelX, KonusTopY + 14, "Konus", "end");
        Beschriftung(sb, ZonenLabelX, WandTopY + 14, "Schachtwand", "end");
        Beschriftung(sb, ZonenLabelX, SohleTopY + 14, "Sohle", "end");

        // --- Fliesspfeil (Zulauf links -> Ablauf rechts) ---
        var pfeilY = (WandTopY + WandBottomY) / 2d;
        var pfeilH = 14d;
        var pfeilL = 18d;
        sb.Append($"<polygon points='{Svg(CenterX - pfeilL)},{Svg(pfeilY - pfeilH)} {Svg(CenterX - pfeilL)},{Svg(pfeilY + pfeilH)} " +
                  $"{Svg(CenterX + pfeilL)},{Svg(pfeilY)}' fill='url(#flowGrad)' stroke='white' stroke-width='1.5' filter='url(#flowGlow)'/>");

        // --- Zu-/Ablauf-Stummel: hoechstens zwei je Seite beschriftet, Rest als "+n". ---
        var marken = new List<SchachtgrafikMarke>();
        Stummelseite(zulaeufe, links: true);
        Stummelseite(ablaeufe, links: false);

        // --- Schaeden je Zone (Anschluss teilt sich die Zeichenflaeche der Schachtwand) ---
        ZeichneSchaeden(schaeden, SchachtZone.Konus, KonusTopY + 35, KonusBottomY - 10);
        ZeichneSchaeden(schaeden, SchachtZone.Schachtwand, WandTopY + 55, WandBottomY - 25, auchZone: SchachtZone.Anschluss);
        ZeichneSchaeden(schaeden, SchachtZone.Sohle, SohleTopY + 40, SohleBottomY - 10);

        // --- Tiefen-Masslinie oder Hinweis. Steht AUSSERHALB der Ablauf-Beschriftung, damit sich
        //     Text und Masslinie nie ueberlagern. ---
        if (tiefeMeter is > 0)
        {
            sb.Append($"<line x1='{Svg(MassLinieX)}' y1='{Svg(GroundY)}' x2='{Svg(MassLinieX)}' y2='{Svg(SohleBottomY)}' " +
                      $"stroke='{brand}' stroke-width='{Svg(MasslinieStrich)}'/>");
            sb.Append($"<line x1='{Svg(MassLinieX - 5)}' y1='{Svg(GroundY)}' x2='{Svg(MassLinieX + 5)}' y2='{Svg(GroundY)}' " +
                      $"stroke='{brand}' stroke-width='{Svg(MasslinieStrich)}'/>");
            sb.Append($"<line x1='{Svg(MassLinieX - 5)}' y1='{Svg(SohleBottomY)}' x2='{Svg(MassLinieX + 5)}' y2='{Svg(SohleBottomY)}' " +
                      $"stroke='{brand}' stroke-width='{Svg(MasslinieStrich)}'/>");
            var tiefeText = tiefeMeter.Value.ToString("0.00", CultureInfo.InvariantCulture) + " m";
            var textY = (GroundY + SohleBottomY) / 2d;
            // Senkrecht gedreht: Der Wert braucht so nur die Breite eines Zeichens statt der
            // ganzen Zahl — sonst raegt die Beschriftung ueber den schmalen rechten Rand hinaus.
            sb.Append($"<text x='{Svg(TiefeTextX)}' y='{Svg(textY)}' font-size='{SchriftKlein}' text-anchor='middle' fill='#1F2937' " +
                      $"font-family='sans-serif' transform='rotate(-90 {Svg(TiefeTextX)} {Svg(textY)})'>{EscapeSvgText(tiefeText)}</text>");
        }
        else
        {
            sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(TiefeHinweisY)}' font-size='{SchriftKlein}' text-anchor='middle' " +
                      $"fill='#6B7280' font-family='sans-serif'>Tiefe nicht erfasst</text>");
        }

        // --- Innenmasse unter der Sohle ---
        var hatMasse = !string.IsNullOrWhiteSpace(dimension1Mm) && !string.IsNullOrWhiteSpace(dimension2Mm);
        var masseText = hatMasse ? $"{dimension1Mm!.Trim()} × {dimension2Mm!.Trim()}" : "–";
        sb.Append($"<text x='{Svg(CenterX)}' y='{Svg(InnenmasseY)}' font-size='12' font-weight='600' text-anchor='middle' " +
                  $"fill='#1F2937' font-family='sans-serif'>{EscapeSvgText(masseText)}</text>");

        sb.Append("</svg>");
        return (sb.ToString(), marken);

        void Stummelseite(IReadOnlyList<SchachtgrafikStummel> stummel, bool links)
        {
            var sichtbar = stummel.Take(MaxStummelJeSeite).ToList();
            var rest = stummel.Count - sichtbar.Count;
            var slots = sichtbar.Count + (rest > 0 ? 1 : 0);
            // Erst deutlich unterhalb der Zonenbeschriftung "Schachtwand" beginnen und vor dem
            // unteren Rand aufhoeren, damit sich Beschriftungen nie ueberlagern.
            var positionen = VerteileY(WandTopY + 45, WandBottomY - 28, slots);

            var wandX = links ? WallLeftX : WallRightX;
            var spitzeX = links ? ZulaufStubTipX : AblaufStubTipX;
            var textX = links ? ZulaufStubTipX - 4 : AblaufLabelX;
            var anker = links ? "end" : "start";

            for (var i = 0; i < sichtbar.Count; i++)
            {
                var y = positionen[i];
                sb.Append($"<line x1='{Svg(wandX)}' y1='{Svg(y)}' x2='{Svg(spitzeX)}' y2='{Svg(y)}' " +
                          $"stroke='#6B7280' stroke-width='{Svg(StrichStark)}' stroke-linecap='round'/>");
                sb.Append($"<circle cx='{Svg(spitzeX)}' cy='{Svg(y)}' r='4' fill='#6B7280' stroke='white' stroke-width='1.2'/>");

                var voll = sichtbar[i].Dn is { } dn ? $"{sichtbar[i].Name} {dn}" : sichtbar[i].Name;
                var beschriftung = KuerzeMitEllipse(voll, LabelKuerzeZeichen);
                Beschriftung(sb, textX, y + 4, beschriftung, anker, farbe: "#4B5563");

                // Hinweisflaeche mit dem vollstaendigen, ungekuerzten Text — sichtbar ist nur die
                // gekuerzte Beschriftung.
                marken.Add(new SchachtgrafikMarke(
                    links ? textX - 60 : textX,
                    y - 8d,
                    60d,
                    16d,
                    voll));
            }

            if (rest > 0)
                Beschriftung(sb, textX, positionen[^1] + 4, $"+{rest}", anker, farbe: "#4B5563");
        }

        void ZeichneSchaeden(
            IReadOnlyList<SchachtgrafikSchadenEintrag> liste, SchachtZone zone, double top, double bottom, SchachtZone? auchZone = null)
        {
            var treffer = liste.Where(s => s.Zone == zone || s.Zone == auchZone).ToList();
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
        StringBuilder sb, double x, double y, string text, string anker, int groesse = SchriftKlein, string farbe = "#6B7280")
    {
        sb.Append($"<text x='{Svg(x)}' y='{Svg(y)}' font-size='{groesse}' text-anchor='{anker}' " +
                  $"fill='{farbe}' font-family='sans-serif'>{EscapeSvgText(text)}</text>");
    }
}
