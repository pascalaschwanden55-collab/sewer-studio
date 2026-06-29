using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Loest Streckenschaden-Marker (A01, B02 gemaess DIN EN 13508-2) zum echten
/// VSA-Code auf, der in der Beschreibung eingebettet ist (z.B. "BBC (Harte Ablagerungen...)").
/// Identische Logik aus WinCanDbImportService und IbakExportImportService zusammengefuehrt.
/// </summary>
internal static class ContinuousDefectCodeResolver
{
    /// <summary>
    /// Streckenschaden-Marker: A01, A02, B01, B02, ... (DIN EN 13508-2 Anfang/Ende Streckenschaden).
    /// </summary>
    public static readonly Regex ContinuousDefectMarkerRegex =
        new(@"^[AB]\d{2}$", RegexOptions.Compiled);

    /// <summary>
    /// VSA-Code am Anfang der Beschreibung extrahieren (z.B. "BBCC (Harte Ablagerungen...)").
    /// </summary>
    public static readonly Regex EmbeddedVsaCodeRegex =
        new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    /// <summary>
    /// Prueft ob der Code ein Streckenschaden-Marker (A01, B02 etc.) ist.
    /// Falls ja, wird der echte VSA-Code aus der Beschreibung extrahiert.
    /// Der Aufrufer erhaelt via <paramref name="resolvedDescription"/> die bereinigte Beschreibung
    /// (ohne VSA-Code-Praefix und umgebende Klammern).
    /// </summary>
    /// <param name="code">Normalisierter Code (uppercase).</param>
    /// <param name="description">Rohe Beschreibung aus dem Import.</param>
    /// <param name="resolvedDescription">Bereinigte Beschreibung oder Original.</param>
    /// <returns>Effektiver VSA-Code; unver&auml;nderter <paramref name="code"/> wenn kein Marker.</returns>
    public static string ResolveEffectiveCode(
        string code,
        string? description,
        out string? resolvedDescription)
    {
        resolvedDescription = description;

        if (!ContinuousDefectMarkerRegex.IsMatch(code) || string.IsNullOrWhiteSpace(description))
            return code;

        var match = EmbeddedVsaCodeRegex.Match(description.Trim());
        if (!match.Success)
            return code;

        var vsaCode = match.Groups[1].Value;
        // Beschreibung bereinigen: VSA-Code am Anfang entfernen, umgebende Klammern entfernen
        var rest = description.Trim().Substring(vsaCode.Length).TrimStart(' ', '(');
        if (rest.EndsWith(")"))
            rest = rest.Substring(0, rest.Length - 1);
        resolvedDescription = rest.Trim();
        if (string.IsNullOrWhiteSpace(resolvedDescription))
            resolvedDescription = description;
        return vsaCode;
    }
}
