using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>Eine erzeugte Beschriftung innerhalb der Eigentuemertabellenzelle.</summary>
public sealed record DossierOwnerCellLabel(
    string CellKey,
    string EditorLabel,
    string DefaultText);

/// <summary>
/// Zentrale Ablage der bisher fest erzeugten Texte vor Telefon, Mail und
/// Objektbewohner. Die Werte nutzen die vorhandenen Text- und Formatfelder des
/// Dossiers; dadurch bleibt das gespeicherte Format rueckwaertskompatibel.
/// </summary>
public static class DossierOwnerCellLabels
{
    public static readonly DossierOwnerCellLabel Phone = new(
        "Telefon_Beschriftung", "Beschriftung Telefon", "Tel.:");

    public static readonly DossierOwnerCellLabel Mail = new(
        "Mail_Beschriftung", "Beschriftung Mail", "Mail:");

    public static readonly DossierOwnerCellLabel Occupancy = new(
        "Objektbewohner_Beschriftung", "Beschriftung Objektbewohner", "Objektbewohner:");

    public static IReadOnlyList<DossierOwnerCellLabel> All { get; } =
        [Phone, Mail, Occupancy];

    public static string Text(DossierDefinition dossier, DossierOwnerCellLabel label)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(label);

        return dossier.TextOverrides is not null
            && dossier.TextOverrides.TryGetValue(label.DefaultText, out var own)
                ? own ?? string.Empty
                : label.DefaultText;
    }

    public static IReadOnlyList<DossierTextStyleRange> Styles(
        DossierDefinition dossier,
        DossierOwnerCellLabel label)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(label);

        var text = Text(dossier, label);
        return dossier.FieldStyles is not null
            && dossier.FieldStyles.TryGetValue(label.CellKey, out var stored)
                ? DossierTopicTextFormatting.Normalize(text, stored)
                : Array.Empty<DossierTextStyleRange>();
    }

    public static void SetText(
        DossierDefinition dossier,
        DossierOwnerCellLabel label,
        string? text)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(label);

        dossier.TextOverrides ??= new();
        var value = text ?? string.Empty;
        if (string.Equals(value, label.DefaultText, StringComparison.Ordinal))
            dossier.TextOverrides.Remove(label.DefaultText);
        else
            dossier.TextOverrides[label.DefaultText] = value;
    }

    public static void SetFormatted(
        DossierDefinition dossier,
        DossierOwnerCellLabel label,
        string? text,
        IEnumerable<DossierTextStyleRange>? styles)
    {
        ArgumentNullException.ThrowIfNull(dossier);
        ArgumentNullException.ThrowIfNull(label);

        var value = text ?? string.Empty;
        SetText(dossier, label, value);

        dossier.FieldStyles ??= new();
        var normalized = DossierTopicTextFormatting.Normalize(value, styles);
        if (normalized.Count == 0)
            dossier.FieldStyles.Remove(label.CellKey);
        else
            dossier.FieldStyles[label.CellKey] = normalized.ToList();
    }
}
