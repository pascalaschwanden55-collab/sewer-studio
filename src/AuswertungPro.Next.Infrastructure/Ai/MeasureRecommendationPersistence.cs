using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Bereinigt und migriert die gespeicherten Lerndaten der Massnahmenempfehlung.
/// Datei-I/O bleibt im Service; diese Klasse enthaelt nur pruefbare Datenlogik.
/// </summary>
internal static class MeasureRecommendationPersistence
{
    internal const int CurrentStoreVersion = 3;
    internal const int CurrentModelVersion = 2;

    internal static (MeasureLearningStore Store, bool Changed) SanitizeStore(MeasureLearningStore loaded)
    {
        var changed = loaded.Version != CurrentStoreVersion;
        var store = new MeasureLearningStore();

        foreach (var sample in loaded.LearnedSamples ?? new HashSet<string>(StringComparer.Ordinal))
        {
            if (!TryNormalizeSampleSignature(sample, out var normalizedSample))
            {
                changed = true;
                continue;
            }

            var added = store.LearnedSamples.Add(normalizedSample);
            if (!string.Equals(sample, normalizedSample, StringComparison.Ordinal) || !added)
            {
                changed = true;
            }
        }

        store.ByCode = SanitizeByCode(loaded.ByCode, ref changed);
        store.ByCodeSignature = SanitizeByCodeSignature(loaded.ByCodeSignature, ref changed);
        return (store, changed);
    }

    internal static Dictionary<string, Dictionary<string, int>> CloneByCode(
        IDictionary<string, Dictionary<string, int>> source)
    {
        var changed = false;
        return SanitizeByCode(source, ref changed);
    }

    internal static Dictionary<string, CostAggregate> CloneByCodeSignature(
        IDictionary<string, CostAggregate> source)
    {
        var changed = false;
        return SanitizeByCodeSignature(source, ref changed);
    }

    private static Dictionary<string, Dictionary<string, int>> SanitizeByCode(
        IDictionary<string, Dictionary<string, int>>? source,
        ref bool changed)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var codeEntry in source ?? new Dictionary<string, Dictionary<string, int>>())
        {
            var code = MeasureRecordParser.NormalizeCode(codeEntry.Key);
            if (!MeasureRecordParser.IsMeasureRelevantDamageCode(code))
            {
                changed = true;
                continue;
            }

            if (!string.Equals(codeEntry.Key, code, StringComparison.Ordinal))
                changed = true;

            if (!result.TryGetValue(code, out var measureMap))
            {
                measureMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                result[code] = measureMap;
            }

            foreach (var measureEntry in codeEntry.Value ?? new Dictionary<string, int>())
            {
                var measure = MeasureRecordParser.NormalizeMeasure(measureEntry.Key);
                if (string.IsNullOrWhiteSpace(measure) || measureEntry.Value <= 0)
                {
                    changed = true;
                    continue;
                }

                if (!string.Equals(measureEntry.Key, measure, StringComparison.Ordinal))
                    changed = true;
                measureMap[measure] = measureMap.TryGetValue(measure, out var count)
                    ? count + measureEntry.Value
                    : measureEntry.Value;
            }

            if (measureMap.Count == 0)
            {
                result.Remove(code);
                changed = true;
            }
        }

        return result;
    }

    private static Dictionary<string, CostAggregate> SanitizeByCodeSignature(
        IDictionary<string, CostAggregate>? source,
        ref bool changed)
    {
        var result = new Dictionary<string, CostAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source ?? new Dictionary<string, CostAggregate>())
        {
            var signature = NormalizeCodeSignature(entry.Key);
            if (string.IsNullOrWhiteSpace(signature) || entry.Value is null)
            {
                changed = true;
                continue;
            }

            if (!string.Equals(entry.Key, signature, StringComparison.Ordinal))
                changed = true;

            if (!result.TryGetValue(signature, out var aggregate))
            {
                aggregate = new CostAggregate();
                result[signature] = aggregate;
            }
            else
            {
                changed = true;
            }

            MergeAggregate(aggregate, CloneAggregate(entry.Value));
        }

        return result;
    }

    private static string NormalizeCodeSignature(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var codes = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(MeasureRecordParser.NormalizeCode)
            .Where(MeasureRecordParser.IsMeasureRelevantDamageCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return MeasureRecordParser.BuildCodeSignature(codes);
    }

    private static bool TryNormalizeSampleSignature(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('|', StringSplitOptions.None);
        if (parts.Length != 9 || !Guid.TryParseExact(parts[0], "N", out _))
            return false;

        var codeSignature = NormalizeCodeSignature(parts[1]);
        var measures = MeasureRecordParser.ParseMeasures(parts[2]);
        if (string.IsNullOrWhiteSpace(codeSignature) || measures.Count == 0)
            return false;

        parts[1] = codeSignature;
        parts[2] = string.Join(";", measures);
        normalized = string.Join("|", parts);
        return true;
    }

    private static CostAggregate CloneAggregate(CostAggregate value)
        => new()
        {
            Samples = Math.Max(0, value.Samples),
            TotalCostSum = value.TotalCostSum,
            TotalCostCount = Math.Max(0, value.TotalCostCount),
            InlinerMetersSum = value.InlinerMetersSum,
            InlinerMetersCount = Math.Max(0, value.InlinerMetersCount),
            InlinerStkSum = value.InlinerStkSum,
            InlinerStkCount = Math.Max(0, value.InlinerStkCount),
            AnschluesseVerpressenSum = value.AnschluesseVerpressenSum,
            AnschluesseVerpressenCount = Math.Max(0, value.AnschluesseVerpressenCount),
            ReparaturManschetteSum = value.ReparaturManschetteSum,
            ReparaturManschetteCount = Math.Max(0, value.ReparaturManschetteCount),
            ReparaturKurzlinerSum = value.ReparaturKurzlinerSum,
            ReparaturKurzlinerCount = Math.Max(0, value.ReparaturKurzlinerCount)
        };

    private static void MergeAggregate(CostAggregate target, CostAggregate source)
    {
        target.Samples += source.Samples;
        target.TotalCostSum += source.TotalCostSum;
        target.TotalCostCount += source.TotalCostCount;
        target.InlinerMetersSum += source.InlinerMetersSum;
        target.InlinerMetersCount += source.InlinerMetersCount;
        target.InlinerStkSum += source.InlinerStkSum;
        target.InlinerStkCount += source.InlinerStkCount;
        target.AnschluesseVerpressenSum += source.AnschluesseVerpressenSum;
        target.AnschluesseVerpressenCount += source.AnschluesseVerpressenCount;
        target.ReparaturManschetteSum += source.ReparaturManschetteSum;
        target.ReparaturManschetteCount += source.ReparaturManschetteCount;
        target.ReparaturKurzlinerSum += source.ReparaturKurzlinerSum;
        target.ReparaturKurzlinerCount += source.ReparaturKurzlinerCount;
    }
}

internal sealed class MeasureLearningStore
{
    public int Version { get; set; } = MeasureRecommendationPersistence.CurrentStoreVersion;
    public HashSet<string> LearnedSamples { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, int>> ByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CostAggregate> ByCodeSignature { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class CostAggregate
{
    public int Samples { get; set; }
    public decimal TotalCostSum { get; set; }
    public int TotalCostCount { get; set; }
    public decimal InlinerMetersSum { get; set; }
    public int InlinerMetersCount { get; set; }
    public int InlinerStkSum { get; set; }
    public int InlinerStkCount { get; set; }
    public int AnschluesseVerpressenSum { get; set; }
    public int AnschluesseVerpressenCount { get; set; }
    public int ReparaturManschetteSum { get; set; }
    public int ReparaturManschetteCount { get; set; }
    public int ReparaturKurzlinerSum { get; set; }
    public int ReparaturKurzlinerCount { get; set; }
}

internal sealed class TrainedMeasureModel
{
    public int Version { get; set; } = MeasureRecommendationPersistence.CurrentModelVersion;
    public DateTime TrainedAtUtc { get; set; } = DateTime.UtcNow;
    public int TotalSamples { get; set; }
    public Dictionary<string, Dictionary<string, int>> ByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CostAggregate> ByCodeSignature { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
