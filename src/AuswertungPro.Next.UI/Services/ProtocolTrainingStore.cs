using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Services;

public static class ProtocolTrainingStore
{
    private static readonly JsonSerializerOptions Opt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string StorePath
        => Path.Combine(AppSettings.AppDataDir, "data", "protocol_training.json");

    public static void AddSample(ProtocolEntry entry, string? haltungId)
    {
        var data = Load();
        data.Samples.Add(new ProtocolTrainingSample
        {
            AtUtc = DateTime.UtcNow,
            HaltungId = haltungId ?? "",
            Code = entry.Code ?? "",
            Beschreibung = entry.Beschreibung ?? "",
            MeterStart = entry.MeterStart,
            MeterEnd = entry.MeterEnd,
            IsStreckenschaden = entry.IsStreckenschaden,
            Parameters = entry.CodeMeta?.Parameters is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(entry.CodeMeta.Parameters, StringComparer.OrdinalIgnoreCase)
        });
        Save(data);
    }

    public static IReadOnlyList<ProtocolTrainingSample> LoadRecent(int maxCount)
    {
        var data = Load();
        return data.Samples
            .OrderByDescending(s => s.AtUtc)
            .Take(Math.Max(0, maxCount))
            .ToList();
    }

    private static ProtocolTrainingData Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new ProtocolTrainingData();
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<ProtocolTrainingData>(json, Opt) ?? new ProtocolTrainingData();
        }
        catch
        {
            return new ProtocolTrainingData();
        }
    }

    private static void Save(ProtocolTrainingData data)
    {
        var dir = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(data, Opt);
        File.WriteAllText(StorePath, json);
    }

    public sealed class ProtocolTrainingData
    {
        public List<ProtocolTrainingSample> Samples { get; set; } = new();
    }

    public sealed class ProtocolTrainingSample
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
