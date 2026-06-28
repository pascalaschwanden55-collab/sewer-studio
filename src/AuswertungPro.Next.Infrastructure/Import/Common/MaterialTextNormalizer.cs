using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Normalisiert Rohrmaterial-Texte fuer den Import.
/// Entfernt Mehrfachzeilen und nicht-material-spezifische Tokens (z.B. "Gereinigt Ja"),
/// die WinCan-DB gelegentlich in das Material-Feld schreibt.
/// Identische Logik aus WinCanDbImportService.NormalizeMaterial und
/// M150MdbImportHelper.NormalizeMaterialValue konsolidiert; Null-Handling liegt beim Aufrufer.
/// </summary>
internal static class MaterialTextNormalizer
{
    private static readonly Regex CleanupTokenRegex =
        new(@"(?i)\s*(gereinigt|nicht\s*gereinigt|verschmutzt)\s*(ja|nein)?\s*$",
            RegexOptions.Compiled);

    /// <summary>
    /// Normalisiert einen Materialtext.
    /// Gibt <see langword="null"/> zurueck wenn das Ergebnis leer ist (Null-Handling beim Aufrufer).
    /// </summary>
    /// <param name="raw">Roher Material-Text aus der Datenbank oder Datei.</param>
    /// <returns>Normalisierter Text oder <see langword="null"/>.</returns>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Nur erste Zeile verwenden — WinCan DB haengt manchmal Reinigungsinfo an
        var t = raw.Split('\n')[0].Trim();
        // Nicht-Material-Tokens am Ende entfernen (z.B. "Gereinigt Ja")
        t = CleanupTokenRegex.Replace(t, "").Trim();

        return string.IsNullOrWhiteSpace(t) ? null : t;
    }
}
