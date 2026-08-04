using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Datenklasse fuer einen VSA-Navigationskachel-Eintrag (ohne UI-Abhaengigkeiten).
/// </summary>
public sealed record VsaTileData
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Description { get; init; }
    public string? BadgeText { get; init; }
    public string? BadgeColor { get; init; }
    public bool IsInvalid { get; init; }
    public bool IsFinal { get; init; }
    public bool IsSteuer { get; init; }
    public string? GroupColor { get; init; }
    public string? Icon { get; init; }
    public bool IsSelected { get; init; }
}

/// <summary>
/// Erzeugt VSA-Kachel-Daten fuer alle Navigations-Ebenen (Gruppe, Hauptcode, Char1, Char2).
/// Kapseliert die wiederholte Badge-Farb-Logik (Q-Pflicht-Farben).
/// Keine UI-Abhaengigkeiten.
/// </summary>
public static class VsaTileDataFactory
{
    /// <summary>Farbe fuer Pflichtfeld-Badge (rot).</summary>
    public const string PflichtColor = "#DC2626";

    /// <summary>Farbe fuer optionales Quant-Badge (orange).</summary>
    public const string QuantColor = "#F59E0B";

    /// <summary>
    /// Bestimmt Badge-Text und -Farbe fuer eine QuantField-Regel.
    /// </summary>
    public static (string? BadgeText, string? BadgeColor) GetQuantBadge(QuantField? q1)
    {
        if (q1 is null) return (null, null);
        var text = q1.Einheit ?? "Q";
        var color = q1.Pflicht == "P" ? PflichtColor : QuantColor;
        return (text, color);
    }

    /// <summary>
    /// Erstellt Kachel-Daten fuer eine Gruppen-Ebene.
    /// </summary>
    public static VsaTileData ForGroup(string key, GroupDef grp, bool isSelected = false)
        => new()
        {
            Key = key,
            Label = key,
            Description = grp.Label,
            GroupColor = grp.Color,
            Icon = grp.Icon,
            IsSelected = isSelected
        };

    /// <summary>
    /// Erstellt Kachel-Daten fuer eine Hauptcode-Ebene.
    /// </summary>
    public static VsaTileData ForCode(
        string key,
        VsaCodeDef cd,
        QuantField? q1,
        string? groupColor,
        bool isSelected = false,
        string? catalogLabel = null)
    {
        var (badgeText, badgeColor) = GetQuantBadge(q1);
        return new VsaTileData
        {
            Key = key,
            Label = key,
            Description = PreferCatalogLabel(cd.Label, catalogLabel),
            IsFinal = cd.FinalCode is not null,
            IsSteuer = cd.IsSteuer,
            BadgeText = badgeText,
            BadgeColor = badgeColor,
            GroupColor = groupColor,
            IsSelected = isSelected
        };
    }

    /// <summary>
    /// Erstellt Kachel-Daten fuer eine Char1-Ebene.
    /// Altes Verhalten (vor Refactoring): Badge-Text = q1?.Einheit (kein "Q"-Fallback).
    /// </summary>
    public static VsaTileData ForChar1(
        string key,
        CharDef charDef,
        string codeKey,
        bool xPrefix,
        bool hasC2,
        QuantField? q1,
        string? groupColor,
        bool isSelected = false,
        string? catalogLabel = null,
        string? parentCatalogLabel = null)
    {
        var prefix = xPrefix ? "X" : "";
        var fullCode = $"{codeKey}{prefix}{key}";
        // Char1 zeigt Einheit direkt (kein "Q"-Fallback wie GetQuantBadge) -- alter Pfad
        var badgeText = q1?.Einheit;
        var badgeColor = q1 is null ? null
            : q1.Pflicht == "P" ? PflichtColor : QuantColor;
        return new VsaTileData
        {
            Key = key,
            Label = fullCode,
            Description = hasC2
                ? BuildNavigationDescription(
                    charDef.Label,
                    catalogLabel,
                    parentCatalogLabel)
                : BuildOptionDescription(
                    charDef.Label,
                    catalogLabel,
                    parentCatalogLabel),
            IsFinal = !hasC2,
            BadgeText = badgeText,
            BadgeColor = badgeColor,
            GroupColor = groupColor,
            IsSelected = isSelected
        };
    }

    /// <summary>
    /// Erstellt Kachel-Daten fuer eine Char2-Ebene.
    /// </summary>
    public static VsaTileData ForChar2(
        string key,
        string label,
        string codeKey,
        string? char1Key,
        bool xPrefix,
        bool isInvalid,
        string? groupColor,
        bool isSelected = false,
        string? catalogLabel = null,
        string? parentCatalogLabel = null)
    {
        var prefix = xPrefix ? "X" : "";
        var fullCode = $"{codeKey}{prefix}{char1Key}{key}";
        return new VsaTileData
        {
            Key = key,
            Label = fullCode,
            Description = BuildFinalOptionDescription(label, catalogLabel),
            IsFinal = true,
            IsInvalid = isInvalid,
            GroupColor = groupColor,
            IsSelected = isSelected
        };
    }

    internal static string BuildOptionDescription(
        string fallback,
        string? catalogLabel,
        string? parentCatalogLabel)
    {
        var clearText = ExtractLeafLabel(catalogLabel, parentCatalogLabel);
        if (string.IsNullOrWhiteSpace(clearText))
            return fallback;

        if (LooksLikeAbbreviation(fallback)
            && !Equivalent(fallback, clearText))
        {
            return $"{fallback.Trim()} \u00B7 {clearText}";
        }

        return clearText;
    }

    internal static string BuildNavigationDescription(
        string fallback,
        string? catalogLabel,
        string? parentCatalogLabel)
    {
        if (!string.IsNullOrWhiteSpace(catalogLabel))
            return catalogLabel.Trim();

        if (string.IsNullOrWhiteSpace(parentCatalogLabel)
            || Equivalent(fallback, parentCatalogLabel)
            || fallback.Trim().StartsWith(
                parentCatalogLabel.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        return $"{parentCatalogLabel.Trim()} \u00B7 {fallback.Trim()}";
    }

    internal static string BuildFinalOptionDescription(
        string fallback,
        string? catalogLabel)
        => string.IsNullOrWhiteSpace(catalogLabel)
            ? fallback
            : catalogLabel.Trim();

    private static string PreferCatalogLabel(string fallback, string? catalogLabel)
        => string.IsNullOrWhiteSpace(catalogLabel)
            ? fallback
            : catalogLabel.Trim();

    private static string? ExtractLeafLabel(string? catalogLabel, string? parentCatalogLabel)
    {
        if (string.IsNullOrWhiteSpace(catalogLabel))
            return null;

        var exact = catalogLabel.Trim();
        if (string.IsNullOrWhiteSpace(parentCatalogLabel))
            return exact;

        var parent = parentCatalogLabel.Trim();
        if (!exact.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            return exact;

        var suffix = exact[parent.Length..]
            .Trim()
            .TrimStart(':', ',', '-', '\u2013', '\u2014')
            .Trim();
        // Einige Manifest-Eintraege (z. B. AEDXA = "unbek.") wiederholen nur
        // den Titel des Elterncodes. Dann ist im Katalog kein genauerer
        // Klartext vorhanden und die kuratierte Optionsbezeichnung ist besser.
        return suffix.Length == 0 ? null : suffix;
    }

    private static bool LooksLikeAbbreviation(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is 0 or > 8)
            return false;

        var hasUpperLetter = false;
        foreach (var ch in text)
        {
            if (char.IsUpper(ch))
            {
                hasUpperLetter = true;
                continue;
            }

            if (char.IsDigit(ch) || char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
                continue;

            return false;
        }

        return hasUpperLetter;
    }

    private static bool Equivalent(string left, string right)
        => string.Equals(
            NormalizeComparable(left),
            NormalizeComparable(right),
            StringComparison.Ordinal);

    private static string NormalizeComparable(string value)
        => new string(value
                .Trim()
                .ToLowerInvariant()
                .Replace("\u00E4", "ae", StringComparison.Ordinal)
                .Replace("\u00F6", "oe", StringComparison.Ordinal)
                .Replace("\u00FC", "ue", StringComparison.Ordinal)
                .Replace("\u00DF", "ss", StringComparison.Ordinal)
                .Where(char.IsLetterOrDigit)
                .ToArray());
}
