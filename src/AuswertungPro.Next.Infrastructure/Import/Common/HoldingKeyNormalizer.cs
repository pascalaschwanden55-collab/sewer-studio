using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Normalisiert Haltungsschluessel fuer den Vergleich und die Deduplizierung beim Import.
/// Alle Import-Services verwenden dieselbe Logik, um konsistente Schluessel sicherzustellen.
/// </summary>
internal static class HoldingKeyNormalizer
{
    /// <summary>
    /// Entfernt fuehrende/nachfolgende Leerzeichen, normalisiert Trennzeichen
    /// und ersetzt interne Whitespace-Sequenzen durch leer.
    /// Geeignet fuer WinCan, XTF und M150.
    /// </summary>
    public static string Normalize(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;

        v = Regex.Replace(v, @"\s+", string.Empty);
        v = v.Replace('/', '-');
        v = v.Replace('–', '-'); // en-dash
        v = v.Replace('—', '-'); // em-dash
        return v;
    }

    /// <summary>
    /// Wie <see cref="Normalize"/>, entfernt zusaetzlich IBAK-spezifische Dateinamen-Prefixe
    /// (L__, L_, H__, H_) und SS-Schachtpraefixe, die IBAK-Exporte vor Haltungsnamen setzen.
    /// </summary>
    public static string NormalizeIbak(string? value)
    {
        var v = Normalize(value);
        if (string.IsNullOrEmpty(v))
            return v;

        if (v.StartsWith("L__", StringComparison.OrdinalIgnoreCase))
            v = v[3..];
        else if (v.StartsWith("L_", StringComparison.OrdinalIgnoreCase))
            v = v[2..];
        else if (v.StartsWith("H__", StringComparison.OrdinalIgnoreCase))
            v = v[3..];
        else if (v.StartsWith("H_", StringComparison.OrdinalIgnoreCase))
            v = v[2..];

        v = Regex.Replace(v, @"(?i)(^|-)SS(?=\d)", "$1");

        return v;
    }
}
