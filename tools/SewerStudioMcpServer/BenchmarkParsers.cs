using System.Text.Json;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

// Pure Hilfsklasse fuer die Benchmark-Daten-Transformation.
// Kein IO, kein JSON-Parsing von Dateien — nur Datenumwandlung.
internal static class BenchmarkParsers
{
    /// <summary>
    /// Wandelt ein JsonElement-Paar (Code, [korrekt, gesamt]) aus per_class in einen BenchmarkWeakCode um.
    /// Gibt null zurueck, wenn das Element kein gueltiges Zahlen-Array mit mindestens 2 Eintraegen ist.
    /// </summary>
    public static BenchmarkWeakCode? ParseClassifierPair(string code, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return null;

        var arr = value.EnumerateArray().ToList();
        if (arr.Count < 2
            || arr[0].ValueKind != JsonValueKind.Number
            || arr[1].ValueKind != JsonValueKind.Number
            || !arr[0].TryGetInt32(out var correct)
            || !arr[1].TryGetInt32(out var total))
        {
            return null;
        }

        var accuracy = total == 0 ? 0 : (double)correct / total;
        return new BenchmarkWeakCode(code, correct, total, accuracy);
    }

    /// <summary>
    /// Wandelt eine CSV-Zeile (Spalten bereits aufgetrennt) aus einer *_by_code.csv in einen BenchmarkWeakCode um.
    /// Erwartet mindestens 3 Spalten: [0]=code, [1]=total, [2]=exact_correct.
    /// Gibt null zurueck, wenn Pflichtfelder fehlen oder nicht parsebar sind.
    /// </summary>
    public static BenchmarkWeakCode? ParseByCodeRow(string[] cols)
    {
        if (cols.Length < 3)
            return null;

        var code = cols[0].Trim();
        if (string.IsNullOrWhiteSpace(code)
            || !int.TryParse(cols[1], out var total)
            || !int.TryParse(cols[2], out var exactCorrect))
        {
            return null;
        }

        var accuracy = total == 0 ? 0 : (double)exactCorrect / total;
        return new BenchmarkWeakCode(code, exactCorrect, total, accuracy);
    }
}
