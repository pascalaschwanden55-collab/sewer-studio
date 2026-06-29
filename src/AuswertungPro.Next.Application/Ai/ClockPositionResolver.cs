using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Berechnet die VSA-Lage am Umfang (Uhrlage) eines Befunds aus seiner Bbox-Geometrie relativ zur
/// kalibrierten Rohrmitte und wendet die VSA-Werte-Konvention an (VSA 2.1.6):
/// - Punktbefund: ein Wert (Mitte), zweiter Wert = 0 (=&gt; "N 00").
/// - Bereich am Umfang: von..bis im Uhrzeigersinn.
/// - Ganzer Umfang: 12 12.
/// - Unbekannt / nicht bestimmbar: 0 0 (=&gt; "00 00").
///
/// Reine, testbare Logik (keine UI/Infrastructure). Winkel: 12 Uhr = oben, im Uhrzeigersinn,
/// aus Kamerasicht in Inspektionsrichtung — identische Konvention wie PipeCalibration.PointToClockHour.
/// </summary>
public static class ClockPositionResolver
{
    /// <summary>Mindest-Bbox-Anteil je Dimension, ab dem ein die Rohrmitte umschliessender Befund als ganzer Umfang (12 12) gilt.</summary>
    public const double FullCircumferenceBoxFill = 0.80;

    /// <summary>Liegt das Bbox-Zentrum naeher als das an der Rohrmitte (normiert), ist die Uhrlage unbestimmt.</summary>
    public const double CentralRadiusNorm = 0.06;

    /// <summary>
    /// VSA-Lage am Umfang als Stundenpaar. 0 bedeutet "kein Wert" (Punkt-Zweitwert bzw. unbekannt).
    /// IsUnknown=true =&gt; "00 00". IsFullCircumference=true =&gt; "12 12".
    /// </summary>
    public readonly record struct ClockSpan(int FromHour, int ToHour, bool IsUnknown, bool IsFullCircumference)
    {
        public static ClockSpan Unknown => new(0, 0, IsUnknown: true, IsFullCircumference: false);
        public static ClockSpan Full => new(12, 12, IsUnknown: false, IsFullCircumference: true);
        public static ClockSpan Point(int hour) => new(hour, 0, IsUnknown: false, IsFullCircumference: false);
        public static ClockSpan Range(int from, int to) => new(from, to, IsUnknown: false, IsFullCircumference: false);
    }

    /// <summary>Normierte Bbox (0..1) eines Befunds.</summary>
    public readonly record struct NormBox(double X1, double Y1, double X2, double Y2)
    {
        public double CenterX => (X1 + X2) / 2.0;
        public double CenterY => (Y1 + Y2) / 2.0;
    }

    /// <summary>
    /// Bestimmt die Uhrlage eines Befunds.
    /// </summary>
    /// <param name="box">Befund-Bbox normiert (0..1).</param>
    /// <param name="pipeCenterX">Rohrmitte X normiert.</param>
    /// <param name="pipeCenterY">Rohrmitte Y normiert.</param>
    /// <param name="isCalibrated">Ist die Rohrmitte kalibriert? Wenn nicht -&gt; Unknown (00 00).</param>
    /// <param name="mainCode">VSA-Hauptcode (3 Buchstaben) fuer code-spezifische Regeln.</param>
    public static ClockSpan Resolve(NormBox box, double pipeCenterX, double pipeCenterY, bool isCalibrated, string? mainCode)
    {
        // Ohne Kalibrierung ist die Rohrmitte unbekannt -> Uhrlage nicht verlaesslich.
        if (!isCalibrated)
            return ClockSpan.Unknown;

        var main = MainCode(mainCode);

        // Ganzer Umfang (12 12) ZUERST pruefen: Eine achsenparallele Bbox kann mit ihren vier Ecken
        // NIE mehr als ~270 Grad aufspannen (Diagonal-Ecken), egal wie gross — Vollumfang ist also
        // nicht ueber die Eckspanne erkennbar. Konservatives, ehrliches Signal: die Box umschliesst
        // die Rohrmitte klar UND fuellt nahezu den ganzen Frame (>80 % beider Dimensionen).
        // Diese Pruefung MUSS vor der Zentral-Pruefung stehen: eine den Frame fuellende Box hat ihr
        // Zentrum exakt in der Rohrmitte und wuerde sonst faelschlich als "zentral unbekannt" gelten.
        bool enclosesCenter = box.X1 <= pipeCenterX && pipeCenterX <= box.X2
                           && box.Y1 <= pipeCenterY && pipeCenterY <= box.Y2;
        if (enclosesCenter
            && (box.X2 - box.X1) >= FullCircumferenceBoxFill
            && (box.Y2 - box.Y1) >= FullCircumferenceBoxFill)
            return ClockSpan.Full;

        // Zentral am Fluchtpunkt (kleine Box mittig): kein eindeutiger Winkel -> unbekannt.
        double cdx = box.CenterX - pipeCenterX;
        double cdy = box.CenterY - pipeCenterY;
        double centerDist = Math.Sqrt(cdx * cdx + cdy * cdy);
        if (centerDist < CentralRadiusNorm)
            return ClockSpan.Unknown;

        // Punktcodes: VSA verlangt einen Wert fuer die Mitte (BCA Anschlussmitte).
        // BAJ (verschobene Rohrverbindung): VSA = Richtung des Versatzes; aus der Maske ist nur die
        // Lage bestimmbar -> wie Punkt (Bbox-Mitte) behandeln (Versatzrichtung kann der Mensch korrigieren).
        bool isPointCode = main is "BCA" or "BAJ";
        if (isPointCode)
            return ClockSpan.Point(ToHour12(HourAt(box.CenterX, box.CenterY, pipeCenterX, pipeCenterY)));

        // Bereich aus den vier Bbox-Ecken: kleinster umschliessender Bogen (zyklisch).
        var hours = new[]
        {
            HourAt(box.X1, box.Y1, pipeCenterX, pipeCenterY),
            HourAt(box.X2, box.Y1, pipeCenterX, pipeCenterY),
            HourAt(box.X2, box.Y2, pipeCenterX, pipeCenterY),
            HourAt(box.X1, box.Y2, pipeCenterX, pipeCenterY)
        };

        var (fromHour, toHour, spanDeg) = SmallestArc(hours);

        // Schmale Spanne (< ~1 Stunde) -> Punktbefund an der Mitte.
        if (spanDeg < 25.0)
            return ClockSpan.Point(ToHour12(HourAt(box.CenterX, box.CenterY, pipeCenterX, pipeCenterY)));

        return ClockSpan.Range(ToHour12(fromHour), ToHour12(toHour));
    }

    /// <summary>Formatiert das Stundenpaar als VSA-Transferwert "VV BB" (zweistellig, 00 = leer/unbekannt).</summary>
    public static string Format(ClockSpan span)
        => $"{span.FromHour:00} {span.ToHour:00}";

    /// <summary>Einzelwert "N:00" fuer das CodeMeta-Feld vsa.uhr.von (null bei unbekannt).</summary>
    public static string? FormatFrom(ClockSpan span)
        => span.IsUnknown ? null : $"{span.FromHour}:00";

    /// <summary>Einzelwert "N:00" fuer das CodeMeta-Feld vsa.uhr.bis (null wenn kein Zweitwert).</summary>
    public static string? FormatTo(ClockSpan span)
        => (span.IsUnknown || span.ToHour == 0) ? null : $"{span.ToHour}:00";

    // ── Geometrie (rein, identisch zu PipeCalibration.PointToClockHour) ──

    /// <summary>Punkt -&gt; Uhrlage 0..12 (12 oben, im Uhrzeigersinn, Y waechst nach unten).</summary>
    private static double HourAt(double x, double y, double cx, double cy)
    {
        double dx = x - cx;
        double dy = y - cy;
        double angleRad = Math.Atan2(dx, -dy);
        double angleDeg = angleRad * 180.0 / Math.PI;
        if (angleDeg < 0) angleDeg += 360.0;
        return angleDeg / 30.0;
    }

    /// <summary>Stunde (0..12) auf 1..12 runden (0 -&gt; 12).</summary>
    private static int ToHour12(double hour)
    {
        int h = (int)Math.Round(hour) % 12;
        return h == 0 ? 12 : h;
    }

    /// <summary>
    /// Kleinster umschliessender Bogen ueber vier zyklische Stundenwerte (0..12).
    /// Liefert (fromHour, toHour, spanGrad), wobei from..to im Uhrzeigersinn der kuerzeste Bogen ist,
    /// der alle Punkte enthaelt.
    /// </summary>
    private static (double From, double To, double SpanDeg) SmallestArc(double[] hours)
    {
        // In Grad umrechnen und sortieren.
        var deg = new double[hours.Length];
        for (int i = 0; i < hours.Length; i++)
            deg[i] = hours[i] * 30.0;
        Array.Sort(deg);

        // Groesste Luecke zwischen aufeinanderfolgenden Punkten (zyklisch) -> der Bogen ist das Komplement.
        double maxGap = 0;
        int gapIndex = 0;
        for (int i = 0; i < deg.Length; i++)
        {
            double next = (i + 1 < deg.Length) ? deg[i + 1] : deg[0] + 360.0;
            double gap = next - deg[i];
            if (gap > maxGap) { maxGap = gap; gapIndex = i; }
        }

        // Der umschliessende Bogen beginnt nach der groessten Luecke.
        double startDeg = deg[(gapIndex + 1) % deg.Length];
        double endDeg = deg[gapIndex];
        double spanDeg = 360.0 - maxGap;

        return (startDeg / 30.0, endDeg / 30.0, spanDeg);
    }

    private static string? MainCode(string? code) => VsaCodeNormalizer.MainCode(code);
}
