using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Mapped ein <see cref="PhotoMeasurementResult"/> auf editierbare Eingabefelder.
/// Reine, testbare Logik – kein UI-Bezug.
/// </summary>
public static class PhotoMeasurementResultMapper
{
    /// <summary>
    /// Ergebnis der Auswertung eines <see cref="PhotoMeasurementResult"/>.
    /// Alle Felder sind null, wenn kein Wert vorhanden ist.
    /// </summary>
    /// <param name="Q1Value">
    ///   Anzeigestring fuer das Q1-Textfeld (Prioritaet: ArcDegrees > FillPercent > Q1Mm).
    /// </param>
    /// <param name="ClockVon">
    ///   Stunden-Anteil der Uhrlage Von als zweistelliger String (z. B. "06"),
    ///   oder null wenn kein Wert vorhanden.
    ///   HINWEIS: Der Minuten-Anteil von ClockFrom wird bewusst nicht uebernommen –
    ///   er wurde im Original nie verwendet (toter Code).
    /// </param>
    public sealed record MappedFields(string? Q1Value, string? ClockVon);

    /// <summary>
    /// Leitet Eingabefelder aus dem PhotoAssistant-Ergebnis ab.
    /// </summary>
    public static MappedFields Map(PhotoMeasurementResult result)
    {
        string? q1Value = null;
        string? clockVon = null;

        var geo = result.Geometry;
        if (geo is null)
            return new MappedFields(null, null);

        // Q1-Wert: FillPercent oder Q1Mm als Ausgangswert
        if (geo.FillPercent != null)
            q1Value = geo.FillPercent.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        else if (geo.Q1Mm != null)
            q1Value = geo.Q1Mm.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        // Uhrlage Von: nur Stunden-Anteil (Minuten-Anteil war im Original toter Code)
        if (geo.ClockFrom != null)
        {
            int hours = (int)geo.ClockFrom.Value;
            clockVon = $"{hours:D2}";
        }

        // ArcDegrees hat Vorrang vor FillPercent/Q1Mm (ueberschreibt Q1 wenn gesetzt)
        if (geo.ArcDegrees != null)
            q1Value = geo.ArcDegrees.Value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

        return new MappedFields(q1Value, clockVon);
    }
}
