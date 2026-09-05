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
                Version = MeasureRecommendationPersistence.CurrentModelVersion,
                TrainedAtUtc = DateTime.UtcNow,
                TotalSamples = sampleCount,
                ByCode = MeasureRecommendationPersistence.CloneByCode(_store.ByCode),
                ByCodeSignature = MeasureRecommendationPersistence.CloneByCodeSignature(_store.ByCodeSignature)
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

        var shouldPersistMigration = false;
        try
        {
            var json = File.ReadAllText(_storePath);
            var loaded = JsonSerializer.Deserialize<MeasureLearningStore>(json, JsonOptions);
            if (loaded is null)
                return;
            if (loaded.Version > MeasureRecommendationPersistence.CurrentStoreVersion)
            {
                BestEffort.ReportWarning(
                    $"[MeasureRecommendationService] Lernspeicher-Version {loaded.Version} ist neuer als unterstuetzt ({MeasureRecommendationPersistence.CurrentStoreVersion}).");
                return;
            }
            (_store, shouldPersistMigration) = MeasureRecommendationPersistence.SanitizeStore(loaded);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[MeasureRecommendationService] Fehler beim Laden: {ex.Message}");
            _store = new MeasureLearningStore();
            return;
        }

        if (shouldPersistMigration)
        {
            try
            {
                // AtomicTextFileWriter legt beim Ersetzen automatisch eine .bak-Datei an.
                SaveUnsafe();
            }
            catch (Exception ex)
            {
                // Die bereinigten Werte bleiben im Speicher nutzbar. Ein Schreibfehler
                // darf Empfehlungen nicht wieder auf die ungefilterten Daten zuruecksetzen.
                BestEffort.ReportWarning($"[MeasureRecommendationService] Migration konnte nicht gespeichert werden: {ex.Message}");
            }
        }
    }

    private void SaveUnsafe()
    {
        var dir = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var saveModel = new MeasureLearningStore
        {
            Version = MeasureRecommendationPersistence.CurrentStoreVersion,
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
        if (_modelLastWriteUtc == lastWriteUtc)
            return _model is not null;

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

            // Modelle der alten Version koennen die fehlerhaften Meter-/BC-Codes
            // enthalten. Sie werden bewusst nicht mehr verwendet; bis zum naechsten
            // Training dient der bereits bereinigte Lernspeicher als Quelle.
            if (loaded.Version < MeasureRecommendationPersistence.CurrentModelVersion)
            {
                _model = null;
                _modelLastWriteUtc = lastWriteUtc;
                return false;
            }

            loaded.ByCode = MeasureRecommendationPersistence.CloneByCode(loaded.ByCode);
            loaded.ByCodeSignature = MeasureRecommendationPersistence.CloneByCodeSignature(loaded.ByCodeSignature);

            _model = loaded;
            _modelLastWriteUtc = lastWriteUtc;
            return true;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[MeasureRecommendationService] Modell-Laden fehlgeschlagen: {ex.Message}");
            _model = null;
            _modelLastWriteUtc = lastWriteUtc;
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

}
