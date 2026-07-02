using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed class MeasureRecommendationService : IMeasureRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _storePath;
    private readonly string _modelPath;
    private readonly object _sync = new();
    private MeasureLearningStore _store = new();
    private TrainedMeasureModel? _model;
    private DateTime? _modelLastWriteUtc;
    private bool _loaded;

    public MeasureRecommendationService(string storePath, string modelPath)
    {
        _storePath = storePath;
        _modelPath = modelPath;
    }

    public MeasureRecommendationResult Recommend(HaltungRecord record, int maxSuggestions = 5)
    {
        if (record is null || maxSuggestions <= 0)
            return MeasureRecommendationResult.Empty;

        var codes = ExtractDamageCodes(record);
        if (codes.Count == 0)
            return MeasureRecommendationResult.Empty;

        lock (_sync)
        {
            EnsureLoaded();
            var hasTrainedModel = TryLoadModelUnsafe();
            var byCode = hasTrainedModel && _model is not null ? _model.ByCode : _store.ByCode;
            var byCodeSignature = hasTrainedModel && _model is not null ? _model.ByCodeSignature : _store.ByCodeSignature;

            var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in codes)
            {
                if (!byCode.TryGetValue(code, out var measuresForCode))
                    continue;

                foreach (var kv in measuresForCode)
                    scores[kv.Key] = scores.TryGetValue(kv.Key, out var existing) ? existing + kv.Value : kv.Value;
            }

            var measures = scores
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Take(maxSuggestions)
                .Select(x => x.Key)
                .ToList();

            if (measures.Count == 0)
                return MeasureRecommendationResult.Empty;

            var codeSignature = BuildCodeSignature(codes);
            if (!byCodeSignature.TryGetValue(codeSignature, out var aggregate))
                return new MeasureRecommendationResult(
                    measures,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    hasTrainedModel);

            var total = AverageDecimal(aggregate.TotalCostSum, aggregate.TotalCostCount, 2);
            var inlinerM = AverageDecimal(aggregate.InlinerMetersSum, aggregate.InlinerMetersCount, 2);
            var inlinerStk = AverageInt(aggregate.InlinerStkSum, aggregate.InlinerStkCount);
            var anschluesse = AverageInt(aggregate.AnschluesseVerpressenSum, aggregate.AnschluesseVerpressenCount);
            var manschette = AverageInt(aggregate.ReparaturManschetteSum, aggregate.ReparaturManschetteCount);
            var kurzliner = AverageInt(aggregate.ReparaturKurzlinerSum, aggregate.ReparaturKurzlinerCount);

            return new MeasureRecommendationResult(
                measures,
                total,
                inlinerM,
                inlinerStk,
                anschluesse,
                manschette,
                kurzliner,
                aggregate.Samples,
                hasTrainedModel);
        }
    }

    public MeasureLearningStats GetStats()
    {
        lock (_sync)
        {
            EnsureLoaded();
            var hasTrainedModel = TryLoadModelUnsafe();
            return new MeasureLearningStats(
                _store.LearnedSamples.Count,
                _store.ByCode.Count,
                _store.ByCodeSignature.Count,
                hasTrainedModel,
                hasTrainedModel ? _model?.TotalSamples : null,
                hasTrainedModel ? _model?.TrainedAtUtc : null,
                _modelPath);
        }
    }

    public MeasureModelTrainingResult TrainModel(int minSamples = 25)
    {
        if (minSamples < 1)
            minSamples = 1;

        lock (_sync)
        {
            EnsureLoaded();
            var sampleCount = _store.LearnedSamples.Count;
            if (sampleCount < minSamples)
            {
                return new MeasureModelTrainingResult(
                    false,
                    sampleCount,
                    minSamples,
                    _modelPath,
                    null,
                    "Zu wenige Trainingsfaelle");
            }

            var model = new TrainedMeasureModel
            {
                Version = 1,
                TrainedAtUtc = DateTime.UtcNow,
                TotalSamples = sampleCount,
                ByCode = CloneByCode(_store.ByCode),
                ByCodeSignature = CloneByCodeSignature(_store.ByCodeSignature)
            };

            try
            {
                SaveModelUnsafe(model);
                _model = model;
                _modelLastWriteUtc = File.GetLastWriteTimeUtc(_modelPath);
                return new MeasureModelTrainingResult(
                    true,
                    sampleCount,
                    minSamples,
                    _modelPath,
                    model.TrainedAtUtc,
                    null);
            }
            catch (Exception ex)
            {
                return new MeasureModelTrainingResult(
                    false,
                    sampleCount,
                    minSamples,
                    _modelPath,
                    null,
                    ex.Message);
            }
        }
    }

    public bool Learn(HaltungRecord record)
    {
        if (record is null || !IsUserConfirmed(record))
            return false;

        var codes = ExtractDamageCodes(record);
        var measures = ParseMeasures(record.GetFieldValue("Empfohlene_Sanierungsmassnahmen"));
        if (codes.Count == 0 || measures.Count == 0)
            return false;

        var costs = ExtractCostSnapshot(record);
        costs = SanitizeCosts(costs);

        var signature = BuildSampleSignature(record.Id, codes, measures, costs);

        lock (_sync)
        {
            EnsureLoaded();

            if (_store.LearnedSamples.Contains(signature))
                return false;

            _store.LearnedSamples.Add(signature);

            foreach (var code in codes)
            {
                if (!_store.ByCode.TryGetValue(code, out var perMeasure))
                {
                    perMeasure = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _store.ByCode[code] = perMeasure;
                }

                foreach (var measure in measures)
                    perMeasure[measure] = perMeasure.TryGetValue(measure, out var count) ? count + 1 : 1;
            }

            var codeSignature = BuildCodeSignature(codes);
            if (!_store.ByCodeSignature.TryGetValue(codeSignature, out var aggregate))
            {
                aggregate = new CostAggregate();
                _store.ByCodeSignature[codeSignature] = aggregate;
            }

            aggregate.Samples++;
            if (costs.TotalCost is > 0)
            {
                aggregate.TotalCostSum += costs.TotalCost.Value;
                aggregate.TotalCostCount++;
            }
            if (costs.InlinerMeters is > 0)
            {
                aggregate.InlinerMetersSum += costs.InlinerMeters.Value;
                aggregate.InlinerMetersCount++;
            }
            if (costs.InlinerStk is > 0)
            {
                aggregate.InlinerStkSum += costs.InlinerStk.Value;
                aggregate.InlinerStkCount++;
            }
            if (costs.AnschluesseVerpressen is > 0)
            {
                aggregate.AnschluesseVerpressenSum += costs.AnschluesseVerpressen.Value;
                aggregate.AnschluesseVerpressenCount++;
            }
            if (costs.ReparaturManschette is > 0)
            {
                aggregate.ReparaturManschetteSum += costs.ReparaturManschette.Value;
                aggregate.ReparaturManschetteCount++;
            }
            if (costs.ReparaturKurzliner is > 0)
            {
                aggregate.ReparaturKurzlinerSum += costs.ReparaturKurzliner.Value;
                aggregate.ReparaturKurzlinerCount++;
            }

            SaveUnsafe();
            return true;
        }
    }

    private static MeasureRecordParser.CostSnapshot SanitizeCosts(MeasureRecordParser.CostSnapshot costs)
        => MeasureRecordParser.SanitizeCosts(costs);

    private static decimal? AverageDecimal(decimal sum, int count, int decimals)
        => MeasureRecordParser.AverageDecimal(sum, count, decimals);

    private static int? AverageInt(int sum, int count)
        => MeasureRecordParser.AverageInt(sum, count);

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        _loaded = true;
        if (!File.Exists(_storePath))
            return;

        try
        {
            var json = File.ReadAllText(_storePath);
            var loaded = JsonSerializer.Deserialize<MeasureLearningStore>(json, JsonOptions);
            if (loaded is null)
                return;

            _store.Version = loaded.Version > 0 ? loaded.Version : 2;
            _store.LearnedSamples = loaded.LearnedSamples is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(loaded.LearnedSamples, StringComparer.Ordinal);

            _store.ByCode = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var codeEntry in loaded.ByCode ?? new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase))
            {
                var code = NormalizeCode(codeEntry.Key);
                if (string.IsNullOrWhiteSpace(code))
                    continue;

                var measureMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var measureEntry in codeEntry.Value)
                {
                    var measure = NormalizeMeasure(measureEntry.Key);
                    if (string.IsNullOrWhiteSpace(measure) || measureEntry.Value <= 0)
                        continue;
                    measureMap[measure] = measureEntry.Value;
                }

                if (measureMap.Count > 0)
                    _store.ByCode[code] = measureMap;
            }

            _store.ByCodeSignature = new Dictionary<string, CostAggregate>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in loaded.ByCodeSignature ?? new Dictionary<string, CostAggregate>(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null)
                    continue;
                _store.ByCodeSignature[entry.Key] = entry.Value;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MeasureRecommendationService] Fehler beim Laden: {ex.Message}");
            _store = new MeasureLearningStore();
        }
    }

    private void SaveUnsafe()
    {
        var dir = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var saveModel = new MeasureLearningStore
        {
            Version = 2,
            LearnedSamples = new HashSet<string>(_store.LearnedSamples, StringComparer.Ordinal),
            ByCode = _store.ByCode.ToDictionary(
                x => x.Key,
                x => x.Value.ToDictionary(y => y.Key, y => y.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase),
            ByCodeSignature = _store.ByCodeSignature.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase)
        };

        var json = JsonSerializer.Serialize(saveModel, JsonOptions);
        AtomicTextFileWriter.WriteAllText(_storePath, json);
    }

    private bool TryLoadModelUnsafe()
    {
        if (!File.Exists(_modelPath))
        {
            _model = null;
            _modelLastWriteUtc = null;
            return false;
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(_modelPath);
        if (_model is not null && _modelLastWriteUtc == lastWriteUtc)
            return true;

        try
        {
            using var stream = File.OpenRead(_modelPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var modelEntry = zip.GetEntry("model.json");
            if (modelEntry is null)
            {
                _model = null;
                _modelLastWriteUtc = null;
                return false;
            }

            using var modelStream = modelEntry.Open();
            var loaded = JsonSerializer.Deserialize<TrainedMeasureModel>(modelStream, JsonOptions);
            if (loaded is null)
            {
                _model = null;
                _modelLastWriteUtc = null;
                return false;
            }

            loaded.ByCode = CloneByCode(loaded.ByCode);
            loaded.ByCodeSignature = CloneByCodeSignature(loaded.ByCodeSignature);

            _model = loaded;
            _modelLastWriteUtc = lastWriteUtc;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MeasureRecommendationService] Modell-Laden fehlgeschlagen: {ex.Message}");
            _model = null;
            _modelLastWriteUtc = null;
            return false;
        }
    }

    private void SaveModelUnsafe(TrainedMeasureModel model)
    {
        var dir = Path.GetDirectoryName(_modelPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Open(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var entry = zip.CreateEntry("model.json", CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        JsonSerializer.Serialize(entryStream, model, JsonOptions);
    }

    private static Dictionary<string, Dictionary<string, int>> CloneByCode(
        IDictionary<string, Dictionary<string, int>> source)
    {
        var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var codeEntry in source)
        {
            var code = NormalizeCode(codeEntry.Key);
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var measureMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var measureEntry in codeEntry.Value)
            {
                var measure = NormalizeMeasure(measureEntry.Key);
                if (string.IsNullOrWhiteSpace(measure) || measureEntry.Value <= 0)
                    continue;
                measureMap[measure] = measureEntry.Value;
            }

            if (measureMap.Count > 0)
                result[code] = measureMap;
        }

        return result;
    }

    private static Dictionary<string, CostAggregate> CloneByCodeSignature(
        IDictionary<string, CostAggregate> source)
    {
        var result = new Dictionary<string, CostAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in source)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null)
                continue;

            var value = entry.Value;
            result[entry.Key] = new CostAggregate
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
        }
        return result;
    }

    private static string BuildSampleSignature(Guid recordId, IReadOnlyList<string> codes, IReadOnlyList<string> measures, MeasureRecordParser.CostSnapshot costs)
        => MeasureRecordParser.BuildSampleSignature(recordId, codes, measures, costs);

    private static string BuildCodeSignature(IReadOnlyList<string> codes)
        => MeasureRecordParser.BuildCodeSignature(codes);

    private static bool IsUserConfirmed(HaltungRecord record)
    {
        return IsUserEdited(record, "Empfohlene_Sanierungsmassnahmen")
            || IsUserEdited(record, "Kosten")
            || IsUserEdited(record, "Renovierung_Inliner_m")
            || IsUserEdited(record, "Renovierung_Inliner_Stk")
            || IsUserEdited(record, "Anschluesse_verpressen")
            || IsUserEdited(record, "Reparatur_Manschette")
            || IsUserEdited(record, "Reparatur_Kurzliner");
    }

    private static bool IsUserEdited(HaltungRecord record, string field)
        => record.FieldMeta.TryGetValue(field, out var meta) && meta.UserEdited;

    private static MeasureRecordParser.CostSnapshot ExtractCostSnapshot(HaltungRecord record)
    {
        return new MeasureRecordParser.CostSnapshot(
            MeasureRecordParser.TryParseDecimal(record.GetFieldValue("Kosten")),
            MeasureRecordParser.TryParseDecimal(record.GetFieldValue("Renovierung_Inliner_m")),
            MeasureRecordParser.TryParseInt(record.GetFieldValue("Renovierung_Inliner_Stk")),
            MeasureRecordParser.TryParseInt(record.GetFieldValue("Anschluesse_verpressen")),
            MeasureRecordParser.TryParseInt(record.GetFieldValue("Reparatur_Manschette")),
            MeasureRecordParser.TryParseInt(record.GetFieldValue("Reparatur_Kurzliner")));
    }

    private static decimal? TryParseDecimal(string? raw)
        => MeasureRecordParser.TryParseDecimal(raw);

    private static int? TryParseInt(string? raw)
        => MeasureRecordParser.TryParseInt(raw);

    private static List<string> ExtractDamageCodes(HaltungRecord record)
        => MeasureRecordParser.ExtractDamageCodes(record);

    private static List<string> ParseMeasures(string? raw)
        => MeasureRecordParser.ParseMeasures(raw);

    private static string NormalizeMeasure(string? value)
        => MeasureRecordParser.NormalizeMeasure(value);

    private static string NormalizeCode(string? value)
        => MeasureRecordParser.NormalizeCode(value);

    private sealed class MeasureLearningStore
    {
        public int Version { get; set; } = 2;
        public HashSet<string> LearnedSamples { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, Dictionary<string, int>> ByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CostAggregate> ByCodeSignature { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CostAggregate
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

    private sealed class TrainedMeasureModel
    {
        public int Version { get; set; } = 1;
        public DateTime TrainedAtUtc { get; set; } = DateTime.UtcNow;
        public int TotalSamples { get; set; }
        public Dictionary<string, Dictionary<string, int>> ByCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CostAggregate> ByCodeSignature { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

}
