using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Dateibasierter Speicher fuer bestaetigte Protokoll-Lernbeispiele.
/// Schreibt atomar und schuetzt den Lese-Aendern-Schreiben-Ablauf vor Parallelzugriffen.
/// </summary>
public sealed class ProtocolTrainingFileStore : IProtocolTrainingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string> _storagePathProvider;
    private readonly object _sync = new();

    public ProtocolTrainingFileStore()
        : this(() => Path.Combine(
            AppDataPathResolver.Resolve(),
            "data",
            "protocol_training.json"))
    {
    }

    public ProtocolTrainingFileStore(string storagePath)
        : this(() => storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Der Speicherpfad darf nicht leer sein.", nameof(storagePath));
    }

    internal ProtocolTrainingFileStore(Func<string> storagePathProvider)
    {
        _storagePathProvider = storagePathProvider
            ?? throw new ArgumentNullException(nameof(storagePathProvider));
    }

    public string StoragePath => Path.GetFullPath(_storagePathProvider());

    public void AddSample(ProtocolEntry entry, string? haltungId)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            var path = StoragePath;
            var data = Load(path);

            // Bestehende Duplikat-Regel: Haltung + Code + auf 0,1 m gerundete Stationierung.
            var code = entry.Code ?? "";
            var holdingId = haltungId ?? "";
            var meterStart = Math.Round(entry.MeterStart ?? 0, 1);
            var signature = $"{holdingId}|{code}|{meterStart:F1}";
            if (data.Samples.Any(sample =>
                    $"{sample.HaltungId}|{sample.Code}|{Math.Round(sample.MeterStart ?? 0, 1):F1}" == signature))
            {
                return;
            }

            data.Samples.Add(new ProtocolTrainingSampleData
            {
                AtUtc = DateTime.UtcNow,
                HaltungId = holdingId,
                Code = code,
                Beschreibung = entry.Beschreibung ?? "",
                MeterStart = entry.MeterStart,
                MeterEnd = entry.MeterEnd,
                IsStreckenschaden = entry.IsStreckenschaden,
                Parameters = entry.CodeMeta?.Parameters is null
                    ? new Dictionary<string, string>()
                    : new Dictionary<string, string>(
                        entry.CodeMeta.Parameters,
                        StringComparer.OrdinalIgnoreCase)
            });

            Save(path, data);
        }
    }

    public IReadOnlyList<ProtocolAiTrainingSample> LoadRecent(int maxCount)
    {
        lock (_sync)
        {
            return Load(StoragePath).Samples
                .OrderByDescending(sample => sample.AtUtc)
                .Take(Math.Max(0, maxCount))
                .Select(ToTrainingSample)
                .ToList();
        }
    }

    private static ProtocolTrainingData Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new ProtocolTrainingData();

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<ProtocolTrainingData>(json, JsonOptions)
                       ?? new ProtocolTrainingData();
            data.Samples ??= new List<ProtocolTrainingSampleData>();
            return data;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"Protokoll-Trainingsdaten konnten nicht gelesen werden ({path}): " +
                $"{ex.GetType().Name}: {ex.Message}");
            return new ProtocolTrainingData();
        }
    }

    private static void Save(string path, ProtocolTrainingData data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        AtomicTextFileWriter.WriteAllText(path, json);
    }

    private static ProtocolAiTrainingSample ToTrainingSample(ProtocolTrainingSampleData sample) =>
        new(
            AtUtc: sample.AtUtc,
            HaltungId: sample.HaltungId ?? "",
            Code: sample.Code ?? "",
            Beschreibung: sample.Beschreibung ?? "",
            MeterStart: sample.MeterStart,
            MeterEnd: sample.MeterEnd,
            IsStreckenschaden: sample.IsStreckenschaden,
            Parameters: new Dictionary<string, string>(
                sample.Parameters ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase));

    private sealed class ProtocolTrainingData
    {
        public List<ProtocolTrainingSampleData> Samples { get; set; } = new();
    }

    private sealed class ProtocolTrainingSampleData
    {
        public DateTime AtUtc { get; set; }
        public string HaltungId { get; set; } = "";
        public string Code { get; set; } = "";
        public string Beschreibung { get; set; } = "";
        public double? MeterStart { get; set; }
        public double? MeterEnd { get; set; }
        public bool IsStreckenschaden { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
