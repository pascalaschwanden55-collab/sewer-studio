using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Strenger Eintrittsfilter fuer Trainingslabels aus freiem PDF-Text.
/// UI-/KI-Resolver duerfen fallback-toleranter sein; dieser Validator nicht.
/// </summary>
public static partial class VsaCodeValidator
{
    public static bool IsKnownCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized))
            return false;

        var groupKey = normalized[..2];
        if (!VsaCodeTree.Groups.TryGetValue(groupKey, out var group))
            return false;

        var mainKey = normalized[..3];
        return group.Codes.ContainsKey(mainKey);
    }

    // Echte VSA-KEK-Codes: Hauptcode (3 Zeichen) + max. Char1 (1) + Char2 (1) = hoechstens 5.
    private const int MaxKnownCodeLength = 5;

    /// <summary>
    /// Zentrale Normalisierung fuer rohe Codes (z.B. WinCan-OpCode): behaelt nur Buchstaben
    /// (entfernt Punkt-Trenner wie "BCA.F.A" und Meter-/Ziffern-Suffixe wie "BCD0.00.0"),
    /// Grossschreibung, und begrenzt auf die strukturelle Maximallaenge.
    /// Liefert den Code nur, wenn der Hauptcode bekannt ist (<see cref="IsKnownCode"/>) UND die
    /// Laenge plausibel ist. Faengt damit typische Parsing-Artefakte (Punkte, Meter-Suffixe,
    /// ueberlange Muell-Codes wie "BCAFAFOO").
    /// Hinweis: Die Char1/Char2-Kombination selbst wird NICHT vollstaendig gegen den Katalog
    /// geprueft — ein 5-stelliger Code mit gueltigem Hauptcode aber unueblichem Unterzeichen
    /// (z.B. "BCDXY") rutscht durch. Bewusst so, um katalog-unbekannte echte Codes nicht zu verwerfen.
    /// </summary>
    public static string? TryNormalizeKnownCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        // Nur Buchstaben behalten: entfernt Punkt-Trenner (BCA.F.A) UND Meter-/Ziffern-Suffixe (BCD0.00.0).
        var letters = new string(code.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray());

        // Strukturelle Laengengrenze: laengere Strings mit gueltigem Hauptcode-Praefix sind Muell.
        if (letters.Length > MaxKnownCodeLength)
            return null;

        return IsKnownCode(letters) ? letters : null;
    }

    [GeneratedRegex("^[A-Z]{3,8}$")]
    private static partial Regex CodePattern();
}
