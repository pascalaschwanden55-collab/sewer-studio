namespace CadasterDbReader;

/// <summary>
/// Reine Klassifikations-Hilfsmethoden fuer Kanalschadenscodes und Tabellen-Scoring.
/// Kein IO, keine Datenbankzugriffe.
/// </summary>
internal static class CadasterClassification
{
    /// <summary>
    /// Ordnet einen VSA/IBAK-Schadenstationscode einer Trainingskategorie zu.
    /// </summary>
    public static string TrainingCategoryForCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "other";

        var upper = code.Trim().ToUpperInvariant();
        var metaCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BCD", "BCE", "BDA", "BDB", "BDC", "AEC", "AED", "AEF"
        };

        if (metaCodes.Contains(upper))
            return "meta";
        if (upper.StartsWith("BCA", StringComparison.OrdinalIgnoreCase) || upper.StartsWith("BCC", StringComparison.OrdinalIgnoreCase))
            return "bauteil";
        if (upper.StartsWith("BA", StringComparison.OrdinalIgnoreCase) || upper.StartsWith("BB", StringComparison.OrdinalIgnoreCase))
            return "schaden";
        return "other";
    }

    /// <summary>
    /// Berechnet einen Relevanz-Score fuer eine Tabelle gemaess der gesuchten Kandidatenart.
    /// </summary>
    public static int ScoreTable(TableReport table, CandidateKind kind)
    {
        var tableName = table.Name.ToUpperInvariant();
        var columns = table.Columns.Select(c => c.ToUpperInvariant()).ToList();
        var score = 0;

        switch (kind)
        {
            case CandidateKind.Media:
                if (ContainsAny(tableName, "PHOTO", "FOTO", "IMAGE", "PIC", "MEDIA", "MM")) score += 6;
                if (columns.Any(c => ContainsAny(c, "FILE", "PATH", "NAME", "DATEI", "PHOTO", "IMAGE"))) score += 4;
                if (columns.Any(c => ContainsAny(c, "OBJ", "HOLD", "HALT", "SECTION", "PIPE"))) score += 2;
                break;
            case CandidateKind.Observation:
                if (ContainsAny(tableName, "OBS", "SCHAD", "DAMAGE", "DEFECT", "INSPECT", "INSPEK")) score += 6;
                if (columns.Any(c => ContainsAny(c, "CODE", "SCHAD", "DAMAGE", "DEFECT", "OBS"))) score += 5;
                if (columns.Any(c => ContainsAny(c, "DIST", "METER", "TIME", "CLOCK", "UHR"))) score += 3;
                break;
            case CandidateKind.CodeLike:
                if (columns.Any(c => ContainsAny(c, "CODE", "SCHAD", "DEFECT", "DAMAGE", "CLASS"))) score += 5;
                if (columns.Any(c => ContainsAny(c, "DIST", "METER", "TIME", "CLOCK", "UHR"))) score += 2;
                break;
        }

        return score;
    }

    /// <summary>
    /// Prueft, ob ein Text einen der angegebenen Schluesselbegriffe enthaelt (Gross-/Kleinschreibung ignoriert).
    /// </summary>
    public static bool ContainsAny(string text, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gibt die Top-20-Kandidatentabellen fuer die gesuchte Kandidatenart zurueck, absteigend nach Score.
    /// </summary>
    public static List<CandidateTableReport> FindCandidateTables(List<TableReport> tables, CandidateKind kind)
    {
        var result = new List<CandidateTableReport>();
        foreach (var table in tables)
        {
            var score = ScoreTable(table, kind);
            if (score <= 0)
                continue;
            result.Add(new CandidateTableReport(table.Name, table.RowCount, score, table.Columns));
        }

        return result
            .OrderByDescending(t => t.Score)
            .ThenByDescending(t => t.RowCount)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }
}
