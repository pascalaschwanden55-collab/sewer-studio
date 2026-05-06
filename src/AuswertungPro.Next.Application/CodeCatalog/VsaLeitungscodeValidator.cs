using System;

namespace AuswertungPro.Next.Application.CodeCatalog;

/// <summary>
/// Prueft ob ein Code ein gueltiger VSA-Leitungscode ist.
/// Nur B-Codes (BA, BB, BC, BD) und AE-Codes.
/// Keine D-Codes (Schacht), keine WinCan-internen Codes.
/// Punkt-Notation wird automatisch normalisiert.
/// </summary>
public static class VsaLeitungscodeValidator
{
    /// <summary>True wenn <paramref name="code"/> ein gueltiger VSA-Leitungscode ist.</summary>
    public static bool IsValid(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 3)
            return false;

        // Punkt-Notation normalisieren: "BCA.A.A" → "BCAAA"
        var normalized = code.Replace(".", "", StringComparison.Ordinal).ToUpperInvariant();

        // Nur Leitungscodes: B-Gruppe (BA-BD) und AE-Gruppe
        // Keine D-Codes (Schacht: DA, DB, DC, DD)
        var prefix = normalized[..2];
        if (prefix is not ("BA" or "BB" or "BC" or "BD" or "AE"))
            return false;

        // VsaCodeTree muss den Code kennen
        return VsaCodeTree.LookupLabel(normalized) is not null;
    }
}
