using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Loesung eines rohen VSA-Codes in den vollstaendigen Navigationspfad (Gruppe / Hauptcode / Char1 / Char2).
/// Reine Logik, keine UI-Abhaengigkeiten.
/// </summary>
public sealed class VsaCodePathResolver
{
    private readonly IReadOnlyDictionary<string, GroupDef> _groups;
    private readonly GetChar2OptionsDelegate _getChar2Options;

    /// <summary>Delegate-Typ fuer die Char2-Optionsaufloesung (entspricht IVsaCodeSelectionCatalog.GetChar2Options).</summary>
    public delegate IReadOnlyDictionary<string, string>? GetChar2OptionsDelegate(VsaCodeDef codeDef, string char1Key);

    public VsaCodePathResolver(
        IReadOnlyDictionary<string, GroupDef> groups,
        GetChar2OptionsDelegate getChar2Options)
    {
        _groups = groups;
        _getChar2Options = getChar2Options;
    }

    /// <summary>
    /// Normalisiert einen rohen VSA-Code: nur Buchstaben und Ziffern, Grossschreibung.
    /// </summary>
    public static string NormalizeCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return string.Empty;

        var chars = rawCode
            .Trim()
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(chars);
    }

    /// <summary>
    /// Baut den vollstaendigen Code-String aus Hauptcode, CodeDef, Char1 und Char2.
    /// </summary>
    public static string BuildCode(string codeKey, VsaCodeDef codeDef, string? c1Key, string? c2Key)
    {
        if (c1Key is null)
            return codeDef.FinalCode ?? codeKey;

        var prefix = codeDef.XPrefix ? "X" : string.Empty;
        return c2Key is null
            ? $"{codeKey}{prefix}{c1Key}"
            : $"{codeKey}{prefix}{c1Key}{c2Key}";
    }

    /// <summary>
    /// Loest einen rohen Code in Navigationspfad und optionalen Endcode auf.
    /// Gibt false zurueck, wenn der Code nicht im Katalog gefunden werden kann.
    /// </summary>
    public bool TryResolveCodePath(
        string? rawCode,
        out string groupKey,
        out string codeKey,
        out string? c1Key,
        out string? c2Key,
        out int level,
        out string? finalCode)
    {
        groupKey = string.Empty;
        codeKey = string.Empty;
        c1Key = null;
        c2Key = null;
        level = 0;
        finalCode = null;

        var normalized = NormalizeCode(rawCode);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        foreach (var (grpKey, group) in _groups)
        {
            foreach (var (candidateCodeKey, codeDef) in group.Codes)
            {
                if (!normalized.StartsWith(candidateCodeKey, System.StringComparison.Ordinal))
                    continue;

                if (codeDef.FinalCode is not null
                    && string.Equals(
                        normalized,
                        NormalizeCode(codeDef.FinalCode),
                        System.StringComparison.Ordinal))
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    level = 1;
                    finalCode = codeDef.FinalCode;
                    return true;
                }

                var rest = normalized[candidateCodeKey.Length..];

                // Endcode ohne Char1/Char2.
                if (rest.Length == 0)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    level = 1;

                    if (codeDef.FinalCode is not null || codeDef.Char1 is null)
                        finalCode = codeDef.FinalCode ?? candidateCodeKey;
                    else
                        level = 2;

                    return true;
                }

                if (codeDef.Char1 is null)
                    continue;

                if (codeDef.XPrefix && rest.StartsWith("X", System.StringComparison.Ordinal))
                    rest = rest[1..];

                if (rest.Length == 0)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    level = 2;
                    return true;
                }

                var char1 = rest[0].ToString();
                if (!codeDef.Char1.ContainsKey(char1))
                    continue;

                var c2Options = _getChar2Options(codeDef, char1);
                if (rest.Length == 1)
                {
                    groupKey = grpKey;
                    codeKey = candidateCodeKey;
                    c1Key = char1;
                    level = c2Options is null ? 2 : 3;
                    finalCode = c2Options is null ? BuildCode(candidateCodeKey, codeDef, char1, null) : null;
                    return true;
                }

                if (rest.Length != 2 || c2Options is null)
                    continue;

                var char2 = rest[1].ToString();
                if (!c2Options.ContainsKey(char2))
                    continue;

                groupKey = grpKey;
                codeKey = candidateCodeKey;
                c1Key = char1;
                c2Key = char2;
                level = 3;
                finalCode = BuildCode(candidateCodeKey, codeDef, char1, char2);
                return true;
            }
        }

        return false;
    }
}
