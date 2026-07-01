using System.Windows;

namespace AuswertungPro.Next.UI.Views.Pages;

internal static class DataGridColorCellStyleFactory
{
    public static Style? CreateHaltungenStyle(string fieldName)
    {
        return fieldName switch
        {
            "Zustandsklasse" => ZustandsklasseCellStyleFactory.CreateHaltungenStyle(fieldName),
            "Eigentuemer" => ZustandsklasseCellStyleFactory.CreateEigentuemerStyle(fieldName),
            "Pruefungsresultat" => ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(fieldName),
            "Referenzpruefung" => ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(fieldName),
            "Ausgefuehrt_durch" => ZustandsklasseCellStyleFactory.CreateAusgefuehrtDurchStyle(fieldName),
            _ => null
        };
    }

    public static Style? CreateSchaechteStyle(string columnName)
    {
        var normalizedHeader = Normalize(columnName);

        if (normalizedHeader.Contains("zustandsklasse", StringComparison.Ordinal))
            return ZustandsklasseCellStyleFactory.CreateSchaechteStyle(columnName);

        if (normalizedHeader.Contains("eigentuemer", StringComparison.Ordinal) ||
            normalizedHeader.Contains("eigentumer", StringComparison.Ordinal) ||
            normalizedHeader.Contains("eigentum", StringComparison.Ordinal))
            return ZustandsklasseCellStyleFactory.CreateEigentuemerStyle(columnName);

        if ((normalizedHeader.Contains("ausgefuehrt", StringComparison.Ordinal) ||
             normalizedHeader.Contains("ausgefuhrt", StringComparison.Ordinal)) &&
            normalizedHeader.Contains("durch", StringComparison.Ordinal))
            return ZustandsklasseCellStyleFactory.CreateAusgefuehrtDurchStyle(columnName);

        if (normalizedHeader.Contains("pruefung", StringComparison.Ordinal) ||
            normalizedHeader.Contains("dichtheit", StringComparison.Ordinal) ||
            normalizedHeader.Contains("dichtigkeit", StringComparison.Ordinal))
            return ZustandsklasseCellStyleFactory.CreatePruefungsresultatStyle(columnName);

        return null;
    }

    private static string Normalize(string value)
        => (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal);
}
