using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Application.Video;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Liest den Rumpf von POST /qgis/seek: {"haltung": "80475-80462", "meter": 18.75}.
///
/// Fail-closed: Alles, was nicht eindeutig eine Haltung und eine Zahl nennt, wird
/// abgewiesen. Ein halb verstandener Auftrag wuerde das offene Video an eine
/// geratene Stelle schicken.
/// </summary>
internal static class QgisSeekAnfrage
{
    public static bool TryLies(string? body, out VideoSprungAuftrag? auftrag)
    {
        auftrag = null;
        if (string.IsNullOrWhiteSpace(body))
            return false;

        try
        {
            using var dokument = JsonDocument.Parse(body);
            var wurzel = dokument.RootElement;
            if (wurzel.ValueKind != JsonValueKind.Object)
                return false;

            if (!wurzel.TryGetProperty("haltung", out var haltungWert)
                || haltungWert.ValueKind != JsonValueKind.String)
                return false;

            var haltung = haltungWert.GetString();
            if (string.IsNullOrWhiteSpace(haltung))
                return false;

            if (!wurzel.TryGetProperty("meter", out var meterWert) || !TryMeter(meterWert, out var meter))
                return false;

            auftrag = new VideoSprungAuftrag(haltung.Trim(), meter);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Zahl bevorzugt. Eine Zeichenkette wird nur mit Punkt als Dezimaltrenner gelesen —
    /// das ist die Schreibweise von JSON, nicht die des Programms.
    /// </summary>
    private static bool TryMeter(JsonElement wert, out double meter)
    {
        meter = 0.0;
        return wert.ValueKind switch
        {
            JsonValueKind.Number => wert.TryGetDouble(out meter),
            JsonValueKind.String => double.TryParse(
                wert.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out meter),
            _ => false
        };
    }
}
