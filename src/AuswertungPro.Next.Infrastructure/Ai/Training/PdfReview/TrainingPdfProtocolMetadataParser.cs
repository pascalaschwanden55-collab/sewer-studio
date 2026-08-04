using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

internal sealed record TrainingPdfProtocolFinding(
    string VsaCode,
    double ObservationMeter,
    TimeSpan? ObservationTime,
    string Description,
    double MeterStart,
    double MeterEnd,
    bool IsStreckenschaden);

internal sealed record TrainingPdfProtocolMetadata(
    string? HaltungId,
    DateTime? InspectionDate,
    IReadOnlyList<TrainingPdfProtocolFinding> Findings)
{
    /// <summary>
    /// True, wenn genau ein ausdruecklicher Protokolltitel die fachliche
    /// Dokumenthaltung festlegt. Abweichende "Haltung"-Felder sind dann
    /// interne Aliase, keine neuen Abschnitte.
    /// </summary>
    public bool HasAuthoritativeHaltungHeader { get; init; }

    /// <summary>
    /// True, wenn der Dokumenttext mehrere ausdrueckliche Haltungen enthaelt.
    /// Das gilt auch dann, wenn nicht aus jeder Haltung ein Foto erkannt wurde.
    /// </summary>
    public bool IsMultiHaltungDocument { get; init; }

    public TrainingPdfProtocolFinding? FindFinding(
        string code,
        double? meter,
        TimeSpan? time)
    {
        var candidates = Findings
            .Where(finding => string.Equals(
                finding.VsaCode,
                code,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (meter.HasValue)
        {
            candidates = candidates
                .Where(finding =>
                    Math.Abs(finding.ObservationMeter - meter.Value) <= 0.05)
                .ToArray();
        }

        if (time.HasValue)
        {
            candidates = candidates
                .Where(finding =>
                    finding.ObservationTime.HasValue
                    && Math.Abs(
                        (finding.ObservationTime.Value - time.Value).TotalSeconds) < 0.5)
                .ToArray();
        }

        return candidates.Length == 1
            ? candidates[0]
            : null;
    }
}

internal enum TrainingPdfPageHaltungSource
{
    None,
    InspectionTitle,
    PhotoTitle,
    DirectField,
}

internal readonly record struct TrainingPdfPageHaltungResolution(
    string? HaltungId,
    bool IsAmbiguous)
{
    public TrainingPdfPageHaltungSource Source { get; init; }

    /// <summary>
    /// Abweichende interne Haltungsnummern, die auf derselben Seite unter
    /// einem eindeutigen Protokolltitel stehen.
    /// </summary>
    public IReadOnlyList<string> AlternateHaltungIds { get; init; } = [];
}

/// <summary>
/// Liest ausschliesslich fachliche Metadaten aus dem bereits extrahierten PDF-Text.
/// Datei- und Ordnernamen bleiben nur ein nachgelagerter Fallback des Importdienstes.
/// </summary>
internal static partial class TrainingPdfProtocolMetadataParser
{
    [GeneratedRegex(
        @"(?im)^\s*(?<kind>Haltungsinspektion|Haltungsbilder)\s*-\s*(?<date>\d{1,2}\.\d{1,2}\.\d{4})\s*-\s*(?<id>\d[\d.]*[-/]\d[\d.]*)\s*$")]
    private static partial Regex ProtocolHeaderRegex();

    [GeneratedRegex(@"(?im)^\s*---\s*Seite\s+\d+\s*---\s*$")]
    private static partial Regex DocumentPageSeparatorRegex();

    [GeneratedRegex(
        @"(?im)\b(?:Insp(?:ektions)?[.\s-]*datum|Inspektionsdatum)\b[ \t:.\-]{0,80}(?<date>\d{1,2}\.\d{1,2}\.\d{4})\b")]
    private static partial Regex InspectionDateRegex();

    [GeneratedRegex(@"\d[\d.]*[-/]\d[\d.]*")]
    private static partial Regex HaltungIdRegex();

    [GeneratedRegex(
        @"(?im)\b(?:Haltung|Leitung|Haltungsnummer|Haltungs[- ]?Nr\.?)\b[ \t:.\-]{0,80}(?<id>\d[\d.]*[-/]\d[\d.]*)")]
    private static partial Regex LabeledHaltungIdRegex();

    [GeneratedRegex(
        @"(?im)^[^\r\n]{0,240}\bHaltung\b[^\r\n]{0,160}\bNr\.?\s*\r?\n[^\r\n]{0,240}?(?<id>\d[\d.]*[-/]\d[\d.]*)")]
    private static partial Regex TwoLineHaltungTableRegex();

    [GeneratedRegex(
        @"(?im)(?<![\p{L}\p{N}.])(?<id>\d[\d.]*[-/]\d[\d.]*)_[A-F0-9][A-F0-9_-]{7,}",
        RegexOptions.IgnoreCase)]
    private static partial Regex PhotoHoldingPrefixRegex();

    public static TrainingPdfProtocolMetadata Parse(string? documentText)
    {
        var text = documentText ?? string.Empty;
        var sectionIds = CollectDocumentSectionIds(text);
        var inspectionHeaderIds = CollectHeaderIds(
            text,
            TrainingPdfPageHaltungSource.InspectionTitle);
        return new TrainingPdfProtocolMetadata(
            ResolveDocumentHaltungId(text, sectionIds),
            ResolveInspectionDate(text),
            TrainingPdfProtocolFindingParser.Parse(text))
        {
            HasAuthoritativeHaltungHeader =
                sectionIds.Count == 1
                && inspectionHeaderIds.Count == 1,
        };
    }

    /// <summary>
    /// Importvariante fuer Sammel-PDFs. Mehrere Protokolltitel sind nur dann
    /// zulaessig, wenn einer davon sicher zur Datei-/Ordnerhaltung passt.
    /// Die einzelnen Fotos erhalten zusaetzlich ihre lokale Abschnitts-ID.
    /// </summary>
    public static TrainingPdfProtocolMetadata ParseForPhotoImport(
        string? documentText,
        string? preferredPathHaltungId)
    {
        var text = documentText ?? string.Empty;
        var sectionIds = CollectDocumentSectionIds(text);
        string? haltungId;
        if (sectionIds.Count == 1)
        {
            haltungId = sectionIds[0];
        }
        else if (sectionIds.Count == 0)
        {
            haltungId = ResolveDocumentHaltungId(text, sectionIds);
        }
        else
        {
            var matching = sectionIds
                .Where(id => TrainingPdfHaltungId.AreEquivalent(
                    id,
                    preferredPathHaltungId))
                .ToArray();
            if (matching.Length != 1)
            {
                throw new InvalidDataException(
                    "Das Sammel-PDF enthaelt mehrere Haltungen, aber keine davon passt eindeutig zum Datei- und Ordnernamen.");
            }

            haltungId = TrainingPdfHaltungId.PreferCleanAlias(
                preferredPathHaltungId,
                matching[0]);
        }

        var isMultiHaltungDocument = sectionIds.Count > 1;
        var inspectionHeaderIds = CollectHeaderIds(
            text,
            TrainingPdfPageHaltungSource.InspectionTitle);
        return new TrainingPdfProtocolMetadata(
            haltungId,
            ResolveInspectionDate(
                text,
                allowMultipleDates: isMultiHaltungDocument),
            TrainingPdfProtocolFindingParser.Parse(text))
        {
            HasAuthoritativeHaltungHeader =
                sectionIds.Count == 1
                && inspectionHeaderIds.Count == 1,
            IsMultiHaltungDocument = isMultiHaltungDocument,
        };
    }

    /// <summary>
    /// Ermittelt die Haltung einer einzelnen PDF-Seite. Ein eindeutiger
    /// Protokolltitel gewinnt gegen interne Leitungs-/Kamera-Aliase.
    /// </summary>
    public static TrainingPdfPageHaltungResolution ResolvePageHaltung(
        string? pageText)
    {
        var text = pageText ?? string.Empty;
        var inspectionIds = CollectHeaderIds(
            text,
            TrainingPdfPageHaltungSource.InspectionTitle);
        var photoIds = CollectHeaderIds(
            text,
            TrainingPdfPageHaltungSource.PhotoTitle);
        var directIds = CollectSingleLineLabeledIds(text);
        var twoLineIds = CollectTwoLineLabeledIds(text);
        if (inspectionIds.Count > 1 || photoIds.Count > 1)
            return new TrainingPdfPageHaltungResolution(null, true);

        if (inspectionIds.Count == 1)
        {
            var canonical = inspectionIds[0];
            var aliases = twoLineIds
                .Where(id => !TrainingPdfHaltungId.AreEquivalent(
                    id,
                    canonical))
                .ToArray();
            var additionalIds = photoIds
                .Concat(directIds)
                .Where(id => !TrainingPdfHaltungId.AreEquivalent(
                    id,
                    canonical))
                .Where(id => !aliases.Any(alias =>
                    TrainingPdfHaltungId.AreEquivalent(alias, id)))
                .ToArray();
            if (additionalIds.Length > 0)
                return new TrainingPdfPageHaltungResolution(null, true);

            return new TrainingPdfPageHaltungResolution(canonical, false)
            {
                Source = TrainingPdfPageHaltungSource.InspectionTitle,
                AlternateHaltungIds = aliases,
            };
        }

        if (photoIds.Count == 1)
        {
            var photoId = photoIds[0];
            if (directIds
                .Concat(twoLineIds)
                .Any(id => !TrainingPdfHaltungId.AreEquivalent(id, photoId)))
            {
                return new TrainingPdfPageHaltungResolution(null, true);
            }

            return new TrainingPdfPageHaltungResolution(photoId, false)
            {
                Source = TrainingPdfPageHaltungSource.PhotoTitle,
            };
        }

        var fieldIds = new List<string>();
        foreach (var id in directIds.Concat(twoLineIds))
            AddExplicitId(fieldIds, id);
        return fieldIds.Count switch
        {
            0 => new TrainingPdfPageHaltungResolution(null, false),
            1 => new TrainingPdfPageHaltungResolution(fieldIds[0], false)
            {
                Source = TrainingPdfPageHaltungSource.DirectField,
            },
            _ => new TrainingPdfPageHaltungResolution(null, true),
        };
    }

    private static string? ResolveDocumentHaltungId(
        string text,
        IReadOnlyList<string>? knownSectionIds = null)
    {
        var sectionIds = knownSectionIds ?? CollectDocumentSectionIds(text);
        if (sectionIds.Count > 1)
        {
            throw new InvalidDataException(
                "Das PDF enthaelt widerspruechliche ausdrueckliche Haltungs-IDs. Der Import wurde zur Sicherheit abgebrochen.");
        }

        if (sectionIds.Count == 1)
            return sectionIds[0];

        // Foto-IDs und freie Nummernpaare sind nur ein Fallback. Ihre Haeufigkeit
        // darf eine ausdrueckliche Angabe aus Protokollkopf oder Leitungsfeld nie
        // ueberstimmen.
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in HaltungIdRegex().Matches(text))
            AddScore(scores, match.Value, 1);
        foreach (Match match in PhotoHoldingPrefixRegex().Matches(text))
            AddScore(scores, match.Groups["id"].Value, 20);

        var ranked = scores
            .Where(pair => pair.Value >= 2)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (ranked.Length == 0)
            return null;
        if (ranked.Length > 1 && ranked[0].Value == ranked[1].Value)
        {
            throw new InvalidDataException(
                "Das PDF enthaelt mehrere gleich starke Haltungs-IDs. Der Import wurde zur Sicherheit abgebrochen.");
        }

        return ranked[0].Key;
    }

    private static IReadOnlyList<string> CollectDocumentSectionIds(string text)
    {
        var sectionIds = new List<string>();
        var aliases = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var conflictingAliases = new List<string>();
        var knownCanonicalSections = new List<string>();
        var pages = DocumentPageSeparatorRegex()
            .Split(text)
            .Where(page => !string.IsNullOrWhiteSpace(page));
        foreach (var page in pages)
        {
            var inspectionIds = CollectHeaderIds(
                page,
                TrainingPdfPageHaltungSource.InspectionTitle);
            var photoIds = CollectHeaderIds(
                page,
                TrainingPdfPageHaltungSource.PhotoTitle);
            var directIds = CollectSingleLineLabeledIds(page);
            var twoLineIds = CollectTwoLineLabeledIds(page);

            foreach (var inspectionId in inspectionIds)
            {
                RegisterDocumentCanonical(
                    knownCanonicalSections,
                    aliases,
                    conflictingAliases,
                    inspectionId);
                AddExplicitId(sectionIds, inspectionId);
            }
            if (inspectionIds.Count == 1)
            {
                RegisterTrustedAliases(
                    aliases,
                    conflictingAliases,
                    knownCanonicalSections,
                    inspectionIds[0],
                    twoLineIds);
            }
            else
            {
                foreach (var twoLineId in twoLineIds)
                {
                    AddExplicitId(
                        sectionIds,
                        ResolveDocumentSectionId(
                            knownCanonicalSections,
                            aliases,
                            conflictingAliases,
                            twoLineId));
                }
            }

            foreach (var directId in directIds)
            {
                if (photoIds.Count > 0 && inspectionIds.Count == 0)
                {
                    AddExplicitId(
                        sectionIds,
                        ResolveDocumentSectionId(
                            knownCanonicalSections,
                            aliases,
                            conflictingAliases,
                            directId));
                }
                else
                {
                    RegisterDocumentCanonical(
                        knownCanonicalSections,
                        aliases,
                        conflictingAliases,
                        directId);
                    AddExplicitId(sectionIds, directId);
                }
            }
            foreach (var photoId in photoIds)
            {
                AddExplicitId(
                    sectionIds,
                    ResolveDocumentSectionId(
                        knownCanonicalSections,
                        aliases,
                        conflictingAliases,
                        photoId));
            }
        }

        return sectionIds;
    }

    private static IReadOnlyList<string> CollectHeaderIds(
        string text,
        TrainingPdfPageHaltungSource source)
    {
        var ids = new List<string>();
        var expectedKind = source switch
        {
            TrainingPdfPageHaltungSource.InspectionTitle =>
                "Haltungsinspektion",
            TrainingPdfPageHaltungSource.PhotoTitle =>
                "Haltungsbilder",
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };
        foreach (Match match in ProtocolHeaderRegex().Matches(text))
        {
            if (string.Equals(
                    match.Groups["kind"].Value,
                    expectedKind,
                    StringComparison.OrdinalIgnoreCase))
            {
                AddExplicitId(ids, match.Groups["id"].Value);
            }
        }

        return ids;
    }

    private static IReadOnlyList<string> CollectSingleLineLabeledIds(string text)
    {
        var ids = new List<string>();
        foreach (Match match in LabeledHaltungIdRegex().Matches(text))
            AddExplicitId(ids, match.Groups["id"].Value);
        return ids;
    }

    private static IReadOnlyList<string> CollectTwoLineLabeledIds(string text)
    {
        var ids = new List<string>();
        foreach (Match match in TwoLineHaltungTableRegex().Matches(text))
            AddExplicitId(ids, match.Groups["id"].Value);
        return ids;
    }

    private static void RegisterTrustedAliases(
        IDictionary<string, string> aliases,
        IList<string> conflictingAliases,
        IReadOnlyList<string> knownCanonicalSections,
        string canonical,
        IReadOnlyList<string> candidates)
    {
        var normalizedCandidates = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(candidate, canonical))
                AddExplicitId(normalizedCandidates, candidate);
        }

        var hasConflict = normalizedCandidates.Any(candidate =>
            IsConflictingAlias(conflictingAliases, candidate)
            || (TryResolveDocumentCanonical(
                    knownCanonicalSections,
                    candidate,
                    out var knownCanonical)
                && !TrainingPdfHaltungId.AreEquivalent(
                    knownCanonical,
                    canonical))
            || (TryResolveAlias(aliases, candidate, out var existingCanonical)
                && !TrainingPdfHaltungId.AreEquivalent(
                    existingCanonical,
                    canonical)));
        if (hasConflict)
        {
            foreach (var candidate in normalizedCandidates)
            {
                RemoveEquivalentAlias(aliases, candidate);
                AddExplicitId(conflictingAliases, candidate);
            }

            return;
        }

        foreach (var candidate in normalizedCandidates)
        {
            RemoveEquivalentAlias(aliases, candidate);
            aliases[candidate] = canonical;
        }
    }

    private static string ResolveDocumentSectionId(
        IList<string> knownCanonicalSections,
        IDictionary<string, string> aliases,
        IList<string> conflictingAliases,
        string candidate)
    {
        if (TryResolveDocumentCanonical(
                knownCanonicalSections,
                candidate,
                out var knownCanonical))
        {
            return knownCanonical;
        }

        if (!IsConflictingAlias(conflictingAliases, candidate)
            && TryResolveAlias(aliases, candidate, out var aliasCanonical))
        {
            return aliasCanonical;
        }

        if (!IsConflictingAlias(conflictingAliases, candidate))
        {
            RegisterDocumentCanonical(
                knownCanonicalSections,
                aliases,
                conflictingAliases,
                candidate);
        }

        return TrainingPdfHaltungId.NormalizeForStorage(candidate)
               ?? candidate;
    }

    private static void RegisterDocumentCanonical(
        IList<string> knownCanonicalSections,
        IDictionary<string, string> aliases,
        IList<string> conflictingAliases,
        string candidate)
    {
        RemoveEquivalentAlias(aliases, candidate);
        for (var index = conflictingAliases.Count - 1; index >= 0; index--)
        {
            if (TrainingPdfHaltungId.AreEquivalent(
                    conflictingAliases[index],
                    candidate))
            {
                conflictingAliases.RemoveAt(index);
            }
        }

        if (TryResolveDocumentCanonical(
                knownCanonicalSections,
                candidate,
                out _))
        {
            return;
        }

        var normalized = TrainingPdfHaltungId.NormalizeForStorage(candidate);
        if (normalized is not null)
            knownCanonicalSections.Add(normalized);
    }

    private static bool TryResolveDocumentCanonical(
        IEnumerable<string> knownCanonicalSections,
        string candidate,
        out string canonical)
    {
        foreach (var known in knownCanonicalSections)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(known, candidate))
                continue;

            canonical = TrainingPdfHaltungId.PreferCleanAlias(
                known,
                candidate)!;
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    private static bool TryResolveAlias(
        IEnumerable<KeyValuePair<string, string>> aliases,
        string candidate,
        out string canonical)
    {
        foreach (var pair in aliases)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(pair.Key, candidate))
                continue;

            canonical = pair.Value;
            return true;
        }

        canonical = string.Empty;
        return false;
    }

    private static bool IsConflictingAlias(
        IEnumerable<string> conflictingAliases,
        string candidate)
        => conflictingAliases.Any(alias =>
            TrainingPdfHaltungId.AreEquivalent(alias, candidate));

    private static void RemoveEquivalentAlias(
        IDictionary<string, string> aliases,
        string candidate)
    {
        var key = aliases.Keys.FirstOrDefault(alias =>
            TrainingPdfHaltungId.AreEquivalent(alias, candidate));
        if (key is not null)
            aliases.Remove(key);
    }

    private static void AddExplicitId(IList<string> ids, string raw)
    {
        var normalized = TrainingPdfHaltungId.NormalizeForStorage(raw);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        for (var index = 0; index < ids.Count; index++)
        {
            if (!TrainingPdfHaltungId.AreEquivalent(ids[index], normalized))
                continue;

            ids[index] = TrainingPdfHaltungId.PreferCleanAlias(
                ids[index],
                normalized)!;
            return;
        }

        ids.Add(normalized);
    }

    private static void AddScore(
        IDictionary<string, int> scores,
        string raw,
        int score)
    {
        var normalized = TrainingPdfHaltungId.NormalizeForStorage(raw);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var equivalentKey = scores.Keys.FirstOrDefault(existing =>
            TrainingPdfHaltungId.AreEquivalent(existing, normalized));
        if (equivalentKey is null)
        {
            scores[normalized] = score;
            return;
        }

        var combinedScore = scores[equivalentKey] + score;
        var preferredKey = TrainingPdfHaltungId.PreferCleanAlias(
            equivalentKey,
            normalized)!;
        if (!string.Equals(
                equivalentKey,
                preferredKey,
                StringComparison.OrdinalIgnoreCase))
        {
            scores.Remove(equivalentKey);
        }

        scores[preferredKey] = combinedScore;
    }

    private static DateTime? ResolveInspectionDate(
        string text,
        bool allowMultipleDates = false)
    {
        var headerDates = ProtocolHeaderRegex()
            .Matches(text)
            .Select(match => ParseDate(match.Groups["date"].Value))
            .Where(date => date.HasValue)
            .Select(date => date!.Value.Date)
            .Distinct()
            .ToArray();
        if (headerDates.Length > 1)
        {
            if (allowMultipleDates)
                return null;

            throw new InvalidDataException(
                "Das PDF enthaelt widerspruechliche Inspektionsdaten.");
        }

        if (headerDates.Length == 1)
            return headerDates[0];

        var labeledDates = InspectionDateRegex()
            .Matches(text)
            .Select(match => ParseDate(match.Groups["date"].Value))
            .Where(date => date.HasValue)
            .Select(date => date!.Value.Date)
            .Distinct()
            .ToArray();
        if (labeledDates.Length > 1)
        {
            if (allowMultipleDates)
                return null;

            throw new InvalidDataException(
                "Das PDF enthaelt widerspruechliche Inspektionsdaten.");
        }

        return labeledDates.Length == 1
            ? labeledDates[0]
            : null;
    }

    private static DateTime? ParseDate(string raw)
        => DateTime.TryParseExact(
            raw.Trim(),
            ["d.M.yyyy", "dd.MM.yyyy"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value.Date
            : null;

}
