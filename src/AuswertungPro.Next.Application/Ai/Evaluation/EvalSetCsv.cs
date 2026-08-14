namespace AuswertungPro.Next.Application.Ai.Evaluation;

/// <summary>
/// Gemeinsame CSV-Hilfsmethoden fÃ¼r alle Scorer in dieser Datei.
/// Kein Escape von ";" â€“ das Ã¼bernimmt ProtocolPdfValueFormatting.EscapeCsv separat.
/// </summary>
internal static class EvalSetCsv
{
    internal static string Csv(string value)
    {
        // Formelanfaenge entschaerfen (Gesamtaudit 2026-08-14): Benchmark-CSVs enthalten
        // Befundtexte aus Fremddaten und werden in Excel geoeffnet.
        var text = AuswertungPro.Next.Application.Common.CsvCell.Neutralize(value);

        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return text;

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    internal static string Bool(bool value) => value ? "True" : "False";
}
