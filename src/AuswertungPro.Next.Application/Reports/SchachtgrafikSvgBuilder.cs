using System.Globalization;
using System.Text;
using static AuswertungPro.Next.Application.Reports.ProtocolPdfValueFormatting;
using static AuswertungPro.Next.Application.Reports.ProtocolTextHelpers;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Eine anklickbare Hinweisflaeche ueber einer Schadensmarke oder einer Anschlusskennung der
/// Schachtgrafik. Koordinaten sind SVG-Koordinaten der Grafik (Breite <see cref="SchachtgrafikSvgBuilder.Width"/>).
/// </summary>
public sealed record SchachtgrafikMarke(double X, double Y, double Breite, double Hoehe, string Tooltip);

/// <summary>Fertige Zeichnung: SVG-Text, Masse, Hinweisflaechen und die Legende darunter.</summary>
public sealed record SchachtgrafikZeichnung(
    string Svg,
    int Breite,
    int Hoehe,
    IReadOnlyList<SchachtgrafikMarke> Marken,
    IReadOnlyList<SchachtgrafikLegende> Legende);

/// <summary>
/// Zeichnet die Stammkarte eines Schachts: oben der Schnitt mit Tiefenmassstab, darunter der
/// Grundriss mit dem Auslauf oben (12 Uhr nach VSA). Beide aus dem
/// <see cref="SchachtgrafikModell"/>; der Bauer entscheidet nichts Fachliches mehr.
///
/// Schnitt: Der Tiefenmassstab (110 bis 130 Einheiten je Meter, Ziel 420 Einheiten fuer die
/// ganze Tiefe) bestimmt jede Hoehe — Anschluesse liegen dort, wo ihre Tiefe ist. Der
/// Hauptauslauf geht rechts hinaus, der gegenueberliegende Einlauf kommt links herein; alle
/// anderen Anschluesse sitzen als Kreise auf der Rueckwand, waagrecht nach ihrer Richtung
/// projiziert. Konus (exzentrisch, Steigeisenseite links) ist schematisch und so beschriftet.
/// Ohne Tiefe gibt es keinen Massstab, sondern den Hinweis.
///
/// Grundriss: Oval und Rohrbreiten massstaeblich, Winkel relativ zum Hauptauslauf; ohne
/// Richtungen stehen die Rohre schematisch verteilt (heller Rand, Hinweis). Nordpfeil nur aus
/// echten Koordinaten.
///
/// Die Zeichnung ist 320 Einheiten breit; Beschriftungen sind Kennungen («A1», «E3») und
/// Nummern, der Text steht in der Legende und in den Hinweisflaechen — bei 320 Pixeln Spalte
/// bleibt sonst nichts lesbar. Nur Elemente/Attribute der SVG-Teilmenge (Pfade M/L/C/Q/Z,
/// gestrichelte Kreise als Pfad, Gruppen nur mit Drehung) und nur Farben aus
/// <c>SvgFarbZuordnung</c>. Striche in Akzent-/Muted-Farbe sind Pfade oder mindestens 4 breit.
/// </summary>
public static class SchachtgrafikSvgBuilder
{
    public const int Width = 320;

    /// <summary>Einheiten je Meter Tiefe: 420 fuer die ganze Tiefe, aber nie unter 110 und nie ueber 130.</summary>
    public const double SkalaMin = 110d;
    public const double SkalaMax = 130d;
    public const double SkalaZiel = 420d;

    /// <summary>Hoehe des Schachtkoerpers ohne bekannte Tiefe (kein Massstab).</summary>
    public const double SchematischeTiefePx = 300d;
    public const double GrundrissHoehe = 300d;
    public const double DeckelOkY = 46d;
    public const double RandUnten = 62d;

    private const double CenterX = 168d;
    private const double SkalaX = 34d;
    private const double Wand = 10d;
    private const double Stub = 26d;
    private const double MarkeR = 8d;
    private const int SchriftKlein = 10;
    private const int SchriftNormal = 11;

    // Farben — jede mit Theme-Zuordnung in SvgFarbZuordnung.
    private const string Papier = "#FFFFFF";
    private const string Beton = "#E5E7EB";
    private const string Kante = "#9CA3AF";
    private const string Hilfslinie = "#D1D5DB";
    private const string Gedaempft = "#6B7280";
    private const string TextFarbe = "#1F2937";
    private const string TextLeise = "#4B5563";
    private const string Erde = "#8B7355";

    public static SchachtgrafikZeichnung Baue(SchachtgrafikModell modell, string brand = "#006E9C")
    {
        ArgumentNullException.ThrowIfNull(modell);

        var sb = new StringBuilder();
        var marken = new List<SchachtgrafikMarke>();

        double? skala = modell.TiefeM is { } tiefe && tiefe > 0
            ? Math.Clamp(SkalaZiel / (double)tiefe, SkalaMin, SkalaMax)
            : null;
        var tiefePx = skala is { } s ? (double)modell.TiefeM!.Value * s : SchematischeTiefePx;
        var schnittHoehe = DeckelOkY + tiefePx + RandUnten;
        var hoehe = (int)Math.Ceiling(schnittHoehe + GrundrissHoehe);

        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' width='{Width}' height='{hoehe}' viewBox='0 0 {Width} {hoehe}'>");
        sb.Append($"<rect width='100%' height='100%' fill='{Papier}'/>");
        sb.Append("<defs><pattern id='erde' patternUnits='userSpaceOnUse' width='6' height='6' patternTransform='rotate(45)'>");
        sb.Append($"<line x1='0' y1='0' x2='0' y2='6' stroke='{Erde}' stroke-width='1'/></pattern></defs>");

        var wasser = NutzungsartReportColors.Resolve(modell.Nutzungsart).Accent;
        new Schnitt(modell, skala, tiefePx, wasser).Zeichne(sb, marken);
        new Grundriss(modell, schnittHoehe, wasser).Zeichne(sb, marken);
        sb.Append("</svg>");

        return new SchachtgrafikZeichnung(sb.ToString(), Width, hoehe, marken, Legende(modell));
    }

    /// <summary>Legende unter der Grafik: Anschluesse, Schaeden, Koten, Hinweise.</summary>
    private static List<SchachtgrafikLegende> Legende(SchachtgrafikModell m)
    {
        var legende = new List<SchachtgrafikLegende>();
        foreach (var a in m.Anschluesse)
            legende.Add(new SchachtgrafikLegende(a.Kennung, TextLeise, a.Beschreibung));
        foreach (var s in m.Schaeden)
            legende.Add(new SchachtgrafikLegende(s.Nr.ToString(CultureInfo.InvariantCulture), s.Farbe, s.Tooltip));

        if (m.Koten is { } k && !k.IstLeer)
        {
            var teile = new List<string>();
            if (k.Deckel is { } d) teile.Add("Deckel " + Kote(d));
            if (k.Sohle is { } so) teile.Add("Sohle " + Kote(so));
            foreach (var a in m.Anschluesse.Where(a => a.KoteM is not null))
                teile.Add(a.Kennung + " " + Kote(a.KoteM!.Value));
            if (teile.Count > 0)
                legende.Add(new SchachtgrafikLegende("m", Kante, "Koten m ü. M. (GeoShop): " + string.Join(" · ", teile)));
        }

        foreach (var h in m.Hinweise)
            legende.Add(new SchachtgrafikLegende("·", Kante, h));
        return legende;

        static string Kote(decimal wert) => wert.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static void Marke(StringBuilder sb, List<SchachtgrafikMarke> marken, double x, double y, SchachtgrafikSchaden schaden)
    {
        sb.Append($"<circle cx='{Svg(x)}' cy='{Svg(y)}' r='{Svg(MarkeR)}' fill='{schaden.Farbe}' stroke='{Papier}' stroke-width='1.2'/>");
        sb.Append($"<text x='{Svg(x)}' y='{Svg(y + 3.5)}' font-size='{SchriftKlein}' font-weight='600' text-anchor='middle' " +
                  $"fill='{Papier}' font-family='sans-serif'>{schaden.Nr.ToString(CultureInfo.InvariantCulture)}</text>");
        marken.Add(new SchachtgrafikMarke(x - MarkeR, y - MarkeR, MarkeR * 2, MarkeR * 2, schaden.Tooltip));
    }

    /// <summary>Hat der Anschluss eine Richtung — vermessen (Azimut) oder aus der Uhrlage des Protokolls?</summary>
    private static bool HatRichtung(SchachtgrafikAnschluss a) => a.AzimutGrad is not null || a.UhrGrad is not null;

    /// <summary>
    /// Der Winkel eines Anschlusses im Grundriss mit Auslauf oben: aus dem Azimut, wenn auch der
    /// Auslauf einen hat (vermessen); sonst aus der Uhrlage des Protokolls (12 Uhr = Auslauf nach
    /// VSA); sonst keiner. Vermessen schlaegt Uhrlage — die Handskizze weicht bis 48 Grad ab.
    /// </summary>
    private static double? RelativerWinkel(SchachtgrafikAnschluss a, double? bezugAzimut)
    {
        if (a.AzimutGrad is { } az && bezugAzimut is { } b)
            return SchachtAnschlussRichtung.Relativ(az, b);
        return a.UhrGrad;
    }

    /// <summary>
    /// Der Einlauf, der als Durchlauf dem Auslauf gegenueberliegt: gleicher Durchmesser und, wenn
    /// beide Tiefen bekannt sind, gleiche Tiefe. Ohne Richtungen bekommt er im Grundriss 6 Uhr
    /// und im Schnitt die linke Seite — die Hauptleitung laeuft durch, Hausanschluesse kommen
    /// von der Seite.
    /// </summary>
    private static bool IstDurchlauf(SchachtgrafikAnschluss einlauf, SchachtgrafikAnschluss? auslauf)
    {
        if (einlauf.IstAuslauf || auslauf?.DnMm is not { } dn || !SchachtgrafikModellBuilder.PasstDn(einlauf.DnMm, dn))
            return false;
        if (auslauf.TiefeM is { } ta && einlauf.TiefeM is { } te && Math.Abs(ta - te) > SchachtgrafikModellBuilder.TiefenSpielM)
            return false;
        return true;
    }

    private static void Schrift(StringBuilder sb, double x, double y, string text, string anker, int groesse = SchriftKlein, string farbe = Gedaempft, bool fett = false)
    {
        var gewicht = fett ? " font-weight='600'" : string.Empty;
        sb.Append($"<text x='{Svg(x)}' y='{Svg(y)}' font-size='{groesse}'{gewicht} text-anchor='{anker}' fill='{farbe}' font-family='sans-serif'>{EscapeSvgText(text)}</text>");
    }

    /// <summary>Kreis als Pfad aus vier Kurven — nur so darf er gestrichelt sein (die Teilmenge kennt kein dasharray am Kreis).</summary>
    private static string KreisPfad(double cx, double cy, double r)
    {
        const double k = 0.5523d;
        var kr = k * r;
        return $"M {Svg(cx + r)},{Svg(cy)} " +
               $"C {Svg(cx + r)},{Svg(cy + kr)} {Svg(cx + kr)},{Svg(cy + r)} {Svg(cx)},{Svg(cy + r)} " +
               $"C {Svg(cx - kr)},{Svg(cy + r)} {Svg(cx - r)},{Svg(cy + kr)} {Svg(cx - r)},{Svg(cy)} " +
               $"C {Svg(cx - r)},{Svg(cy - kr)} {Svg(cx - kr)},{Svg(cy - r)} {Svg(cx)},{Svg(cy - r)} " +
               $"C {Svg(cx + kr)},{Svg(cy - r)} {Svg(cx + r)},{Svg(cy - kr)} {Svg(cx + r)},{Svg(cy)} Z";
    }

    private static string Masse(SchachtgrafikModell m)
    {
        if (!m.HatMasse)
            return "Innenmasse nicht erfasst";
        var text = $"{m.Dimension1Mm!.Value.ToString(CultureInfo.InvariantCulture)} × {m.Dimension2Mm!.Value.ToString(CultureInfo.InvariantCulture)} mm";
        return string.IsNullOrWhiteSpace(m.Schachtform) ? text : text + " · " + m.Schachtform.Trim().ToLowerInvariant();
    }

    /// <summary>Der senkrechte Schnitt.</summary>
    private sealed class Schnitt
    {
        private readonly SchachtgrafikModell _m;
        private readonly double? _skala;
        private readonly double _sMm;
        private readonly double _y0 = DeckelOkY;
        private readonly double _yBoden;
        private readonly double _w;
        private readonly double _halb;
        private readonly double _xL;
        private readonly double _xR;
        private readonly double _oeff;
        private readonly bool _hatKonus;
        private readonly double _konusH;
        private readonly double _yKonus;
        private readonly double _bankettY;
        private readonly double _bankettH;
        private readonly double _rinneB;
        private readonly string _wasser;
        private readonly Dictionary<int, (double X, double Y)> _kennungen = new();

        public Schnitt(SchachtgrafikModell m, double? skala, double tiefePx, string wasser)
        {
            _m = m;
            _skala = skala;
            _wasser = wasser;
            var sM = skala ?? 100d;
            _sMm = sM / 1000d;
            _yBoden = _y0 + tiefePx;

            var breiteMm = Math.Max(m.Dimension1Mm ?? 0, m.Dimension2Mm ?? 0);
            if (breiteMm <= 0)
                breiteMm = 1000;
            _w = Math.Clamp(breiteMm * _sMm, 60d, 200d);
            _halb = _w / 2d;
            _xL = CenterX - _halb;
            _xR = CenterX + _halb;

            var oeffMm = m.DeckelDurchmesserMm is > 0 and < 5000 ? m.DeckelDurchmesserMm.Value : 600;
            _oeff = Math.Min(oeffMm * _sMm, _w);
            _hatKonus = _oeff < _w - 2;
            _konusH = _hatKonus ? Math.Min(0.6 * sM, tiefePx * 0.3) : 0;
            _yKonus = _y0 + 10 + _konusH;

            var auslaufDn = m.Hauptauslauf?.DnMm ?? 250;
            _rinneB = Math.Max(14d, auslaufDn * _sMm);
            _bankettH = Math.Max(12d, auslaufDn * _sMm);
            _bankettY = _yBoden - _bankettH;
        }

        public void Zeichne(StringBuilder sb, List<SchachtgrafikMarke> marken)
        {
            Schrift(sb, 12, 16, "Schnitt · Tiefe ab Deckel-OK", "start");

            // Terrain: Schraffur unter der Oberflaeche, die Oberflaeche selbst als schmaler Balken.
            sb.Append($"<rect x='40' y='{Svg(_y0)}' width='240' height='7' fill='url(#erde)'/>");
            sb.Append($"<rect x='40' y='{Svg(_y0 - 1)}' width='240' height='2' fill='{Erde}'/>");

            // Betonkoerper aussen, Hohlraum innen (Konus als Pfad, Schachtwand als Rechteck).
            sb.Append($"<path d='M {Svg(_xL - Wand)},{Svg(_y0 + 10)} L {Svg(_xL + _oeff + Wand)},{Svg(_y0 + 10)} " +
                      $"L {Svg(_xR + Wand)},{Svg(_yKonus)} L {Svg(_xR + Wand)},{Svg(_yBoden + Wand)} " +
                      $"L {Svg(_xL - Wand)},{Svg(_yBoden + Wand)} Z' fill='{Beton}' stroke='{Kante}' stroke-width='1.2'/>");
            if (_hatKonus)
            {
                sb.Append($"<path d='M {Svg(_xL)},{Svg(_y0 + 10)} L {Svg(_xL + _oeff)},{Svg(_y0 + 10)} " +
                          $"L {Svg(_xR)},{Svg(_yKonus)} L {Svg(_xL)},{Svg(_yKonus)} Z' fill='{Papier}' stroke='{Kante}' stroke-width='1'/>");
            }

            sb.Append($"<rect x='{Svg(_xL)}' y='{Svg(_yKonus)}' width='{Svg(_w)}' height='{Svg(_yBoden - _yKonus)}' " +
                      $"fill='{Papier}' stroke='{Kante}' stroke-width='1'/>");

            // Rahmen und Deckel
            sb.Append($"<rect x='{Svg(_xL - 6)}' y='{Svg(_y0)}' width='{Svg(_oeff + 12)}' height='10' fill='{Gedaempft}'/>");
            sb.Append($"<rect x='{Svg(_xL)}' y='{Svg(_y0)}' width='{Svg(_oeff)}' height='5' fill='{TextFarbe}'/>");
            var deckelText = _m.DeckelDurchmesserMm is { } dd
                ? $"Deckel Ø {dd.ToString(CultureInfo.InvariantCulture)}" + (string.IsNullOrWhiteSpace(_m.DeckelMaterial) ? "" : " " + _m.DeckelMaterial.Trim())
                : "Deckel (schematisch)";
            Schrift(sb, _xL + _oeff / 2d, _y0 - 6, deckelText, "middle");
            if (_hatKonus)
                Schrift(sb, _xR + Wand + 6, _y0 + 10 + _konusH * 0.55, "Konus", "start");

            // Steigeisen auf der geraden Seite (schematisch links)
            if (_m.SteigeisenVorhanden == true)
            {
                var schritt = Math.Max(20d, 0.3 * (_skala ?? 100d));
                for (var y = _yKonus + 14; y < _bankettY - 14; y += schritt)
                    sb.Append($"<line x1='{Svg(_xL)}' y1='{Svg(y)}' x2='{Svg(_xL + 12)}' y2='{Svg(y)}' stroke='{TextFarbe}' stroke-width='2.5' stroke-linecap='round'/>");
            }

            // Bankett und Durchlaufrinne
            var rL = CenterX - _rinneB / 2d;
            var rR = CenterX + _rinneB / 2d;
            sb.Append($"<polygon points='{Svg(_xL)},{Svg(_bankettY)} {Svg(rL)},{Svg(_bankettY + 2)} {Svg(rL)},{Svg(_yBoden)} {Svg(_xL)},{Svg(_yBoden)}' fill='{Beton}' stroke='{Kante}'/>");
            sb.Append($"<polygon points='{Svg(rR)},{Svg(_bankettY + 2)} {Svg(_xR)},{Svg(_bankettY)} {Svg(_xR)},{Svg(_yBoden)} {Svg(rR)},{Svg(_yBoden)}' fill='{Beton}' stroke='{Kante}'/>");
            sb.Append($"<path d='M {Svg(rL)},{Svg(_bankettY + 2)} C {Svg(rL)},{Svg(_yBoden + _rinneB * 0.9)} {Svg(rR)},{Svg(_yBoden + _rinneB * 0.9)} {Svg(rR)},{Svg(_bankettY + 2)}' " +
                      $"fill='{_wasser}' opacity='0.25' stroke='{Kante}'/>");
            var wasserY = _yBoden - Math.Min(4d, _bankettH / 3d);
            sb.Append($"<path d='M {Svg(_xL + 2)},{Svg(wasserY)} L {Svg(_xR - 6)},{Svg(wasserY)}' fill='none' stroke='{_wasser}' stroke-width='4' stroke-linecap='round' opacity='0.85'/>");
            sb.Append($"<polygon points='{Svg(_xR - 10)},{Svg(wasserY - 6)} {Svg(_xR - 1)},{Svg(wasserY)} {Svg(_xR - 10)},{Svg(wasserY + 6)}' fill='{_wasser}'/>");

            ZeichneAnschluesse(sb, marken);
            ZeichneMassstab(sb);

            Schrift(sb, CenterX, _yBoden + Wand + 22, Masse(_m), "middle", SchriftNormal, TextLeise, fett: true);
            var tiefeText = _m.TiefeM is { } t
                ? $"Tiefe {t.ToString("0.00", CultureInfo.InvariantCulture)} m" + (_m.TiefeQuelle is null ? "" : $" ({_m.TiefeQuelle})")
                : "Tiefe nicht erfasst";
            Schrift(sb, CenterX, _yBoden + Wand + 38, tiefeText, "middle");

            ZeichneSchaeden(sb, marken);
        }

        private double YSohle(SchachtgrafikAnschluss a)
        {
            if (_skala is { } s && a.TiefeM is { } t)
                return Math.Clamp(_y0 + (double)t * s, _y0 + 12, _yBoden);
            if (_skala is null && !a.IstAuslauf)
                return _y0 + (_yBoden - _y0) * 0.55;
            return _yBoden;
        }

        private void ZeichneAnschluesse(StringBuilder sb, List<SchachtgrafikMarke> marken)
        {
            var haupt = _m.Hauptauslauf;
            var bezug = haupt?.AzimutGrad;
            double? Rel(SchachtgrafikAnschluss a) => RelativerWinkel(a, bezug);

            // Der Einlauf, der dem Auslauf gegenueberliegt, kommt links als Rohr; ohne Richtungen der groesste.
            var einlaeufe = _m.Anschluesse.Where(a => !a.IstAuslauf).ToList();
            var links = einlaeufe
                .Where(a => Rel(a) is { } r && r is >= 135 and <= 225)
                .OrderByDescending(a => a.DnMm ?? 0).ThenBy(a => a.Nr)
                .FirstOrDefault();
            if (links is null && !_m.HatRichtungen)
                links = einlaeufe.OrderBy(a => IstDurchlauf(a, haupt) ? 0 : 1).ThenByDescending(a => a.DnMm ?? 0).ThenBy(a => a.Nr).FirstOrDefault();

            var kreise = 0;
            foreach (var a in _m.Anschluesse.OrderBy(a => a.Nr))
            {
                var ySohle = YSohle(a);
                var h = Math.Max(6d, (a.DnMm ?? 200) * _sMm);
                var ohneTiefe = a.TiefeM is null && _skala is not null;
                var randfarbe = a.ImProjekt ? Kante : Hilfslinie;

                if (ReferenceEquals(a, haupt))
                {
                    sb.Append($"<rect x='{Svg(_xR)}' y='{Svg(ySohle - h)}' width='{Svg(Stub)}' height='{Svg(h)}' fill='{Papier}' stroke='{randfarbe}' stroke-width='1.2'/>");
                    sb.Append($"<polygon points='{Svg(_xR + 8)},{Svg(ySohle - h / 2 - 4)} {Svg(_xR + 16)},{Svg(ySohle - h / 2)} {Svg(_xR + 8)},{Svg(ySohle - h / 2 + 4)}' fill='{_wasser}'/>");
                    Kennung(sb, marken, a, _xR + Stub / 2d, ySohle - h - 5, "middle", ohneTiefe);
                    continue;
                }

                if (ReferenceEquals(a, links))
                {
                    sb.Append($"<rect x='{Svg(_xL - Stub)}' y='{Svg(ySohle - h)}' width='{Svg(Stub)}' height='{Svg(h)}' fill='{Papier}' stroke='{randfarbe}' stroke-width='1.2'/>");
                    sb.Append($"<polygon points='{Svg(_xL - 18)},{Svg(ySohle - h / 2 - 4)} {Svg(_xL - 10)},{Svg(ySohle - h / 2)} {Svg(_xL - 18)},{Svg(ySohle - h / 2 + 4)}' fill='{_wasser}'/>");
                    Kennung(sb, marken, a, _xL - Stub / 2d, ySohle - h - 5, "middle", ohneTiefe);
                    continue;
                }

                // Kreis auf der Rueckwand: waagrecht nach der Richtung, sonst verteilt. Ein hoher
                // Anschluss (E3 bei 0.60 m) liegt im Konus — dort ist der Hohlraum schmaler, der
                // Kreis rueckt hinein, aber nicht nach unten: Seine Tiefe ist gemessen.
                var r = Math.Max(4d, (a.DnMm ?? 150) * _sMm / 2d);
                var rel = Rel(a);
                var cy = Math.Max(_y0 + 10 + r + 2, ySohle - r);
                var cx = rel is { } winkel
                    ? CenterX + Math.Cos(winkel * Math.PI / 180d) * (_halb - r - 4)
                    : CenterX + ((kreise % 3) - 1) * _halb * 0.5;
                kreise++;
                var (innenLinks, innenRechts) = Innenbreite(cy);
                cx = Math.Clamp(cx, innenLinks + r + 3, Math.Max(innenLinks + r + 3, innenRechts - r - 3));
                sb.Append($"<circle cx='{Svg(cx)}' cy='{Svg(cy)}' r='{Svg(r)}' fill='{Papier}' stroke='{randfarbe}' stroke-width='1.5'/>");

                var rechts = cx + r + 22 <= _xR - 2;
                Kennung(sb, marken, a, rechts ? cx + r + 3 : cx - r - 3, cy + 4, rechts ? "start" : "end", ohneTiefe);
            }
        }

        /// <summary>Linke und rechte Innenkante des Hohlraums auf Hoehe <paramref name="y"/> (im Konus schmaler).</summary>
        private (double Links, double Rechts) Innenbreite(double y)
        {
            if (!_hatKonus || y >= _yKonus || _konusH <= 0)
                return (_xL, _xR);

            var anteil = Math.Clamp((y - (_y0 + 10)) / _konusH, 0d, 1d);
            return (_xL, _xL + _oeff + (_w - _oeff) * anteil);
        }

        private void Kennung(StringBuilder sb, List<SchachtgrafikMarke> marken, SchachtgrafikAnschluss a, double x, double y, string anker, bool ohneTiefe)
        {
            var text = ohneTiefe ? a.Kennung + " ?" : a.Kennung;
            Schrift(sb, x, y, text, anker, SchriftKlein, a.ImProjekt ? TextFarbe : TextLeise, fett: true);
            var links = anker switch { "start" => x, "end" => x - 22, _ => x - 12 };
            _kennungen[a.Nr] = (links + 12, y - 4);
            marken.Add(new SchachtgrafikMarke(links, y - 11, 24, 14, a.Beschreibung + (ohneTiefe ? " · Lage im Schnitt ohne Tiefe" : "")));
        }

        private void ZeichneMassstab(StringBuilder sb)
        {
            if (_skala is not { } s || _m.TiefeM is not { } tiefe)
            {
                Schrift(sb, SkalaX + 6, (_y0 + _yBoden) / 2d, "Tiefe nicht erfasst", "middle", SchriftKlein, Gedaempft);
                return;
            }

            sb.Append($"<line x1='{Svg(SkalaX)}' y1='{Svg(_y0)}' x2='{Svg(SkalaX)}' y2='{Svg(_yBoden)}' stroke='{TextFarbe}' stroke-width='1'/>");
            var schritt = tiefe > 6 ? 1.0m : 0.5m;
            for (var wert = 0m; wert < tiefe; wert += schritt)
            {
                var y = _y0 + (double)wert * s;
                if (_yBoden - y < 12)
                    break;
                sb.Append($"<line x1='{Svg(SkalaX - 4)}' y1='{Svg(y)}' x2='{Svg(SkalaX)}' y2='{Svg(y)}' stroke='{TextFarbe}' stroke-width='1'/>");
                Schrift(sb, SkalaX - 7, y + 3.5, wert == 0 ? "0" : wert.ToString("0.0", CultureInfo.InvariantCulture), "end", SchriftKlein, TextLeise);
            }

            sb.Append($"<line x1='{Svg(SkalaX - 6)}' y1='{Svg(_yBoden)}' x2='{Svg(SkalaX)}' y2='{Svg(_yBoden)}' stroke='{TextFarbe}' stroke-width='1.5'/>");
            Schrift(sb, SkalaX - 7, _yBoden + 3.5, tiefe.ToString("0.00", CultureInfo.InvariantCulture), "end", SchriftKlein, TextFarbe, fett: true);
            var mitte = (_y0 + _yBoden) / 2d;
            sb.Append($"<text x='{Svg(SkalaX - 26)}' y='{Svg(mitte)}' font-size='{SchriftKlein}' text-anchor='middle' fill='{Gedaempft}' font-family='sans-serif' " +
                      $"transform='rotate(-90 {Svg(SkalaX - 26)} {Svg(mitte)})'>m ab Deckel-OK</text>");
        }

        private void ZeichneSchaeden(StringBuilder sb, List<SchachtgrafikMarke> marken)
        {
            var belegt = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var schaden in _m.Schaeden)
            {
                var (schluessel, x, y) = Ort(schaden);
                var i = belegt.GetValueOrDefault(schluessel);
                belegt[schluessel] = i + 1;
                Marke(sb, marken, x + 18 * (i % 3), y + 18 * (i / 3), schaden);
            }
        }

        private (string Schluessel, double X, double Y) Ort(SchachtgrafikSchaden s)
        {
            var wandMitte = _yKonus + (_bankettY - _yKonus) * 0.5;
            var rL = CenterX - _rinneB / 2d;
            switch (s.Bauteil)
            {
                case SchachtBauteil.Deckel:
                    return ("deckel", _xL + _oeff + 18, _y0 + 5);
                case SchachtBauteil.Rahmen:
                    return ("rahmen", Math.Max(12, _xL - 18), _y0 + 5);
                case SchachtBauteil.Schachthals:
                {
                    // An der schraegen Seite entlang, damit ein hoher Anschluss (Kreis links im
                    // Konus) und seine Kennung frei bleiben.
                    var y = _hatKonus ? _y0 + 10 + _konusH * 0.3 : _yKonus + 14;
                    return ("hals", Innenbreite(y).Rechts - 14, y);
                }
                case SchachtBauteil.Konus:
                {
                    var y = _hatKonus ? _y0 + 10 + _konusH * 0.72 : _yKonus + 14;
                    return ("konus", Innenbreite(y).Rechts - 14, y);
                }
                case SchachtBauteil.Steigeisen:
                    return ("steig", _xL + 24, _yKonus + (_bankettY - _yKonus) * 0.62);
                case SchachtBauteil.Anschluss:
                    // Ueber der Kennung, nicht auf ihr: Die Marke sitzt eine Zeile hoeher.
                    if (s.AnschlussNr is { } nr && _kennungen.TryGetValue(nr, out var pos))
                        return ("a" + nr, pos.X + 4, pos.Y - 20);
                    return ("anschluss", CenterX + _halb * 0.45, wandMitte - 10);
                case SchachtBauteil.Bankett:
                    return rL - _xL < 20
                        ? ("bankett", rL - 10, _bankettY - 10)
                        : ("bankett", _xL + (rL - _xL) / 2d, _bankettY + Math.Min(9, _bankettH / 2d));
                case SchachtBauteil.Durchlaufrinne:
                    return ("rinne", CenterX, _yBoden - 6);
                case SchachtBauteil.Sohle:
                    return ("sohle", CenterX + _rinneB / 2d + 12, _yBoden - 6);
                case SchachtBauteil.Tauchbogen:
                    return ("tauch", _xR - 12, _bankettY - 10);
                default:
                    return ("wand", CenterX, wandMitte);
            }
        }
    }

    /// <summary>Der Grundriss mit dem Auslauf oben.</summary>
    private sealed class Grundriss
    {
        private readonly SchachtgrafikModell _m;
        private readonly double _top;
        private readonly double _cx = 160d;
        private readonly double _cy;
        private readonly double _rx;
        private readonly double _ry;
        private readonly double _k;
        private readonly double _ring;
        private readonly string _wasser;
        private readonly Dictionary<int, (double X, double Y)> _kennungen = new();

        public Grundriss(SchachtgrafikModell m, double top, string wasser)
        {
            _m = m;
            _top = top;
            _cy = top + 150d;
            _wasser = wasser;
            if (m.HatMasse)
            {
                var d1 = m.Dimension1Mm!.Value;
                var d2 = m.Dimension2Mm!.Value;
                _k = 124d / Math.Max(d1, d2);
                _rx = Math.Min(d1, d2) * _k / 2d;
                _ry = Math.Max(d1, d2) * _k / 2d;
            }
            else
            {
                _k = 0.11d;
                _rx = _ry = 55d;
            }

            _ring = Math.Max(_rx, _ry) + 40d;
        }

        public void Zeichne(StringBuilder sb, List<SchachtgrafikMarke> marken)
        {
            Schrift(sb, 12, _top + 14, "Grundriss · Auslauf oben (12 Uhr)", "start");

            var winkel = Winkel();

            sb.Append($"<path d='{KreisPfad(_cx, _cy, _ring)}' fill='none' stroke='{Hilfslinie}' stroke-dasharray='4,4'/>");
            // «12» steht rechts neben dem Auslauf, der dort immer liegt. Die anderen Stundenmarken
            // weichen einem Rohr an derselben Stelle: Dort steht schon dessen Kennung.
            Schrift(sb, _cx + 26, _cy - _ring + 12, "12", "start", SchriftKlein, TextLeise);
            if (StundeFrei(90d, winkel))
                Schrift(sb, _cx + _ring + 5, _cy + 4, "3", "start", SchriftKlein, TextLeise);
            if (StundeFrei(180d, winkel))
                Schrift(sb, _cx, _cy + _ring + 13, "6", "middle", SchriftKlein, TextLeise);
            if (StundeFrei(270d, winkel))
                Schrift(sb, _cx - _ring - 5, _cy + 4, "9", "end", SchriftKlein, TextLeise);

            // Rohre zuerst, damit der Schachtkoerper ihre Enden ueberdeckt.
            foreach (var (a, theta, echt) in winkel)
            {
                var r = Radius(theta);
                var bw = Math.Max(8d, (a.DnMm ?? 150) * _k);
                var rand = echt && a.ImProjekt ? Kante : Hilfslinie;
                sb.Append($"<g transform='rotate({Svg(theta)} {Svg(_cx)} {Svg(_cy)})'>");
                sb.Append($"<rect x='{Svg(_cx - bw / 2d)}' y='{Svg(_cy - r - 40)}' width='{Svg(bw)}' height='46' fill='{Papier}' stroke='{rand}' stroke-width='1.2'/>");
                if (a.IstAuslauf)
                    sb.Append($"<polygon points='{Svg(_cx)},{Svg(_cy - r - 30)} {Svg(_cx - 5)},{Svg(_cy - r - 20)} {Svg(_cx + 5)},{Svg(_cy - r - 20)}' fill='{_wasser}'/>");
                else
                    sb.Append($"<polygon points='{Svg(_cx)},{Svg(_cy - r - 4)} {Svg(_cx - 5)},{Svg(_cy - r - 14)} {Svg(_cx + 5)},{Svg(_cy - r - 14)}' fill='{_wasser}'/>");
                sb.Append("</g>");
            }

            sb.Append($"<ellipse cx='{Svg(_cx)}' cy='{Svg(_cy)}' rx='{Svg(_rx + 8)}' ry='{Svg(_ry + 8)}' fill='{Beton}' stroke='{Kante}' stroke-width='1.2'/>");
            sb.Append($"<ellipse cx='{Svg(_cx)}' cy='{Svg(_cy)}' rx='{Svg(_rx)}' ry='{Svg(_ry)}' fill='{Papier}' stroke='{Kante}'/>");

            // Gerinne von jedem Einlauf zum Hauptauslauf
            var auslauf = winkel.FirstOrDefault(w => ReferenceEquals(w.Anschluss, _m.Hauptauslauf));
            if (auslauf.Anschluss is not null)
            {
                var (ox, oy) = Innen(auslauf.Theta);
                foreach (var (a, theta, _) in winkel.Where(w => !w.Anschluss.IstAuslauf))
                {
                    var (px, py) = Innen(theta);
                    sb.Append($"<path d='M {Svg(px)},{Svg(py)} Q {Svg(_cx)},{Svg(_cy)} {Svg(ox)},{Svg(oy)}' fill='none' stroke='{_wasser}' stroke-width='4' stroke-linecap='round' opacity='0.85'/>");
                }

                var rA = Radius(auslauf.Theta);
                sb.Append($"<g transform='rotate({Svg(auslauf.Theta)} {Svg(_cx)} {Svg(_cy)})'>");
                sb.Append($"<polygon points='{Svg(_cx)},{Svg(_cy - rA + 1)} {Svg(_cx - 5)},{Svg(_cy - rA + 12)} {Svg(_cx + 5)},{Svg(_cy - rA + 12)}' fill='{_wasser}'/>");
                sb.Append("</g>");
            }

            if (_m.DeckelDurchmesserMm is > 0 and < 5000)
                sb.Append($"<path d='{KreisPfad(_cx, _cy, Math.Min(_m.DeckelDurchmesserMm.Value * _k / 2d, Math.Min(_rx, _ry) - 2))}' fill='none' stroke='{Kante}' stroke-dasharray='3,3'/>");

            // Kennungen an den Rohrenden (nicht mitgedreht)
            foreach (var (a, theta, _) in winkel)
            {
                var rad = theta * Math.PI / 180d;
                var r = Radius(theta) + 52;
                var x = _cx + r * Math.Sin(rad);
                var y = _cy - r * Math.Cos(rad);
                Schrift(sb, x, y + 4, a.Kennung, "middle", SchriftKlein, a.ImProjekt ? TextFarbe : TextLeise, fett: true);
                _kennungen[a.Nr] = (x, y);
                marken.Add(new SchachtgrafikMarke(x - 11, y - 7, 22, 14, a.Beschreibung));
            }

            ZeichneNordpfeil(sb);
            ZeichneSchaeden(sb);

            Schrift(sb, _cx, _top + GrundrissHoehe - 10, Masse(_m), "middle", SchriftKlein, TextLeise);
            var ohneRichtung = _m.HatRichtungen
                ? _m.Anschluesse.Where(a => !HatRichtung(a)).Select(a => a.Kennung).ToList()
                : [];
            if (ohneRichtung.Count > 0)
                Schrift(sb, 12, _top + GrundrissHoehe - 24, "ohne Richtung: " + string.Join(", ", ohneRichtung), "start");
            else if (!_m.HatRichtungen && _m.Anschluesse.Count > 0)
                Schrift(sb, 12, _top + GrundrissHoehe - 24, "Richtungen nicht erfasst: schematisch", "start");
        }

        /// <summary>Winkel im Uhrzeigersinn ab oben je Anschluss; ohne Daten schematisch verteilt.</summary>
        private List<(SchachtgrafikAnschluss Anschluss, double Theta, bool Echt)> Winkel()
        {
            var haupt = _m.Hauptauslauf;
            var bezug = haupt?.AzimutGrad;
            var ergebnis = new List<(SchachtgrafikAnschluss, double, bool)>();
            var schematischeEinlaeufe = new[] { 180d, 120d, 240d, 150d, 210d, 90d, 270d, 60d, 300d };
            var einlauf = 0;
            var auslauf = 0;
            // Ohne Richtungen: der Durchlauf (gleicher Durchmesser und gleiche Tiefe wie der
            // Auslauf) liegt bei 6 Uhr, die uebrigen nach Groesse an den Seiten.
            var einlaeufeNachGroesse = _m.Anschluesse
                .Where(a => !a.IstAuslauf)
                .OrderBy(a => IstDurchlauf(a, haupt) ? 0 : 1)
                .ThenByDescending(a => a.DnMm ?? 0).ThenBy(a => a.Nr)
                .ToList();

            foreach (var a in _m.Anschluesse.OrderBy(a => a.Nr))
            {
                // Vermessen (Azimut relativ zum Auslauf) vor Uhrlage des Protokolls; beides ist echt.
                if (RelativerWinkel(a, bezug) is { } rel)
                {
                    ergebnis.Add((a, rel, true));
                    continue;
                }

                // Sind Richtungen bekannt, bekommt ein Anschluss ohne Richtung KEINEN erfundenen
                // Winkel: Er fehlt im Grundriss und steht unten als «ohne Richtung».
                if (_m.HatRichtungen)
                    continue;

                if (ReferenceEquals(a, haupt))
                {
                    ergebnis.Add((a, 0d, false));
                    continue;
                }

                if (a.IstAuslauf)
                {
                    auslauf++;
                    ergebnis.Add((a, 30d * auslauf, false));
                    continue;
                }

                var rang = einlaeufeNachGroesse.IndexOf(a);
                var theta = schematischeEinlaeufe[Math.Min(Math.Max(rang, 0), schematischeEinlaeufe.Length - 1)];
                einlauf++;
                ergebnis.Add((a, theta, false));
            }

            return ergebnis;
        }

        /// <summary>Wahr, wenn kein gezeichnetes Rohr naeher als 20 Grad an dieser Stundenmarke liegt.</summary>
        private static bool StundeFrei(double stunde, List<(SchachtgrafikAnschluss Anschluss, double Theta, bool Echt)> winkel)
            => winkel.All(w => Math.Abs(((w.Theta - stunde) % 360d + 540d) % 360d - 180d) > 20d);

        private double Radius(double theta)
        {
            var rad = theta * Math.PI / 180d;
            var sx = Math.Sin(rad);
            var cyv = Math.Cos(rad);
            return 1d / Math.Sqrt(sx * sx / (_rx * _rx) + cyv * cyv / (_ry * _ry));
        }

        private (double X, double Y) Innen(double theta)
        {
            var rad = theta * Math.PI / 180d;
            var r = Radius(theta);
            return (_cx + r * Math.Sin(rad), _cy - r * Math.Cos(rad));
        }

        private void ZeichneNordpfeil(StringBuilder sb)
        {
            if (_m.Hauptauslauf?.AzimutGrad is not { } bezug)
                return;

            var relN = SchachtAnschlussRichtung.Relativ(0d, bezug);
            var px = 292d;
            var py = _top + 52d;
            sb.Append($"<g transform='rotate({Svg(relN)} {Svg(px)} {Svg(py)})'>");
            sb.Append($"<line x1='{Svg(px)}' y1='{Svg(py + 14)}' x2='{Svg(px)}' y2='{Svg(py - 8)}' stroke='{TextFarbe}' stroke-width='1.5'/>");
            sb.Append($"<polygon points='{Svg(px)},{Svg(py - 14)} {Svg(px - 5)},{Svg(py - 4)} {Svg(px + 5)},{Svg(py - 4)}' fill='{TextFarbe}'/>");
            sb.Append("</g>");

            var rad = relN * Math.PI / 180d;
            var nx = px + 24 * Math.Sin(rad);
            var ny = py - 24 * Math.Cos(rad);
            Schrift(sb, nx, ny + 4, "N", "middle", SchriftKlein, TextFarbe, fett: true);
        }

        private void ZeichneSchaeden(StringBuilder sb)
        {
            var belegt = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var s in _m.Schaeden)
            {
                (string Schluessel, double X, double Y)? ort = s.Bauteil switch
                {
                    SchachtBauteil.Bankett => ("bankett", _cx - _rx * 0.45, _cy + _ry * 0.3),
                    SchachtBauteil.Durchlaufrinne or SchachtBauteil.Sohle => ("rinne", _cx, _cy + _ry * 0.12),
                    SchachtBauteil.Anschluss when s.AnschlussNr is { } nr && _kennungen.TryGetValue(nr, out var pos)
                        => ("a" + nr, pos.X + 12, pos.Y - 12),
                    _ => null
                };
                if (ort is not { } o)
                    continue;

                var i = belegt.GetValueOrDefault(o.Schluessel);
                belegt[o.Schluessel] = i + 1;
                var x = o.X + 18 * (i % 3);
                var y = o.Y + 18 * (i / 3);
                sb.Append($"<circle cx='{Svg(x)}' cy='{Svg(y)}' r='{Svg(MarkeR)}' fill='{s.Farbe}' stroke='{Papier}' stroke-width='1.2'/>");
                sb.Append($"<text x='{Svg(x)}' y='{Svg(y + 3.5)}' font-size='{SchriftKlein}' font-weight='600' text-anchor='middle' fill='{Papier}' font-family='sans-serif'>{s.Nr.ToString(CultureInfo.InvariantCulture)}</text>");
            }
        }
    }
}
