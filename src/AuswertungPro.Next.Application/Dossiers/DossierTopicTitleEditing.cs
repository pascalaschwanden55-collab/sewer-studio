using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Models.Dossiers;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Eigene Fassung eines Thementitels fuer genau ein Dossier.
///
/// Der Gebietstitel bleibt dabei unveraendert. Gespeichert wird die Fassung in
/// den bereits vorhandenen Feldwerten; dadurch braucht das Dossierformat keine
/// zweite parallele Themenliste und alte Dateien bleiben voll kompatibel.
/// </summary>
public static class DossierTopicTitleEditing
{
    private const string Prefix = "Themen_Titel::";

    public static string SourceTitle(DossierTopicRow topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        return string.IsNullOrWhiteSpace(topic.SourceTitle)
            ? (topic.Title ?? string.Empty).Trim()
            : topic.SourceTitle.Trim();
    }

    public static string DisplayTitle(DossierDefinition? dossier, string? sourceTitle)
    {
        var source = (sourceTitle ?? string.Empty).Trim();
        if (source.Length == 0 || dossier?.FieldOverrides is null)
            return source;

        return dossier.FieldOverrides.TryGetValue(Key(source), out var own)
            ? own ?? string.Empty
            : source;
    }

    public static IReadOnlyList<DossierTextStyleRange> Styles(
        DossierDefinition? dossier,
        string? sourceTitle,
        string? displayTitle = null)
    {
        var source = (sourceTitle ?? string.Empty).Trim();
        var text = displayTitle ?? DisplayTitle(dossier, source);

        return source.Length > 0
            && dossier?.FieldStyles is not null
            && dossier.FieldStyles.TryGetValue(Key(source), out var stored)
                ? DossierTopicTextFormatting.Normalize(text, stored)
                : Array.Empty<DossierTextStyleRange>();
    }

    public static void Set(
        DossierDefinition dossier,
        string? sourceTitle,
        string? displayTitle,
        IEnumerable<DossierTextStyleRange>? styles)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var source = (sourceTitle ?? string.Empty).Trim();
        if (source.Length == 0)
            return;

        var display = displayTitle ?? string.Empty;
        var normalized = DossierTopicTextFormatting.Normalize(display, styles);
        var key = Key(source);

        dossier.FieldOverrides ??= new();
        dossier.FieldStyles ??= new();

        if (string.Equals(display, source, StringComparison.Ordinal)
            && normalized.Count == 0)
        {
            dossier.FieldOverrides.Remove(key);
            dossier.FieldStyles.Remove(key);
            return;
        }

        dossier.FieldOverrides[key] = display;
        if (normalized.Count == 0)
            dossier.FieldStyles.Remove(key);
        else
            dossier.FieldStyles[key] = normalized.ToList();
    }

    public static void Reset(DossierDefinition dossier, string? sourceTitle)
    {
        ArgumentNullException.ThrowIfNull(dossier);

        var source = (sourceTitle ?? string.Empty).Trim();
        if (source.Length == 0)
            return;

        dossier.FieldOverrides?.Remove(Key(source));
        dossier.FieldStyles?.Remove(Key(source));
    }

    public static bool IsOverridden(DossierDefinition? dossier, string? sourceTitle)
    {
        var source = (sourceTitle ?? string.Empty).Trim();
        if (source.Length == 0 || dossier is null)
            return false;

        var key = Key(source);
        return dossier.FieldOverrides?.ContainsKey(key) == true
            || dossier.FieldStyles?.ContainsKey(key) == true;
    }

    private static string Key(string sourceTitle) => Prefix + sourceTitle.Trim();
}
