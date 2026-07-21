namespace AuswertungPro.Next.Application.Ai.Evaluation;

/// <summary>
/// Gemeinsame CSV-Hilfsmethoden fÃ¼r alle Scorer in dieser Datei.
/// Kein Escape von ";" â€“ das Ã¼bernimmt ProtocolPdfValueFormatting.EscapeCsv separat.
/// </summary>
internal static class EvalSetCsv
{
    internal static string Csv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    internal static string Bool(bool value) => value ? "True" : "False";
}
