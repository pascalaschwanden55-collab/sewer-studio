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
        bool isSelected = false)
    {
        var (badgeText, badgeColor) = GetQuantBadge(q1);
        return new VsaTileData
        {
            Key = key,
            Label = key,
            Description = cd.Label,
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
    /// </summary>
    public static VsaTileData ForChar1(
        string key,
        CharDef charDef,
        string codeKey,
        bool xPrefix,
        bool hasC2,
        QuantField? q1,
        string? groupColor,
        bool isSelected = false)
    {
        var prefix = xPrefix ? "X" : "";
        var fullCode = $"{codeKey}{prefix}{key}";
        var (badgeText, badgeColor) = GetQuantBadge(q1);
        return new VsaTileData
        {
            Key = key,
            Label = fullCode,
            Description = charDef.Label,
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
        bool isSelected = false)
    {
        var prefix = xPrefix ? "X" : "";
        var fullCode = $"{codeKey}{prefix}{char1Key}{key}";
        return new VsaTileData
        {
            Key = key,
            Label = fullCode,
            Description = label,
            IsFinal = true,
            IsInvalid = isInvalid,
            GroupColor = groupColor,
            IsSelected = isSelected
        };
    }
}
