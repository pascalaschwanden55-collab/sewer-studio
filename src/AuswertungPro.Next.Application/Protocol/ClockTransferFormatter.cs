namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Formatiert die Transfer-Anzeige fuer Uhrlage-Von/Bis-Felder.
/// Reine, testbare Logik – kein UI-Bezug.
/// </summary>
public static class ClockTransferFormatter
{
    /// <summary>
    /// Erzeugt den Transfer-Anzeigetext aus den Rohwerten der Von/Bis-Textboxen.
    /// Leer oder Whitespace wird als "--" dargestellt, ansonsten wird
    /// auf zwei Stellen mit fuehrender Null linksseitig aufgefuellt.
    /// </summary>
    /// <param name="von">Inhalt des Von-Textfelds (z. B. "6", "", null).</param>
    /// <param name="bis">Inhalt des Bis-Textfelds (z. B. "9", "", null).</param>
    /// <returns>Transfer-Text, z. B. "Transfer: 06 09" oder "Transfer: -- --".</returns>
    public static string Format(string? von, string? bis)
    {
        var vonPart = string.IsNullOrWhiteSpace(von) ? "--" : von.Trim().PadLeft(2, '0');
        var bisPart = string.IsNullOrWhiteSpace(bis) ? "--" : bis.Trim().PadLeft(2, '0');
        return $"Transfer: {vonPart} {bisPart}";
    }
}
