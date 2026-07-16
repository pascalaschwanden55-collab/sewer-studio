using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im injizierbaren
/// <see cref="IProtocolTrainingStore"/>.
/// </summary>
public static class ProtocolTrainingStore
{
    private static readonly IProtocolTrainingStore Default = new ProtocolTrainingFileStore();

    public static string DefaultPath => Current.StoragePath;

    internal static IProtocolTrainingStore Current => Default;

    public static void AddSample(ProtocolEntry entry, string? haltungId) =>
        Current.AddSample(entry, haltungId);

    public static IReadOnlyList<ProtocolTrainingSample> LoadRecent(int maxCount) =>
        Current.LoadRecent(maxCount)
            .Select(sample => new ProtocolTrainingSample
            {
                AtUtc = sample.AtUtc,
                HaltungId = sample.HaltungId,
                Code = sample.Code,
                Beschreibung = sample.Beschreibung,
                MeterStart = sample.MeterStart,
                MeterEnd = sample.MeterEnd,
                IsStreckenschaden = sample.IsStreckenschaden,
                Parameters = new Dictionary<string, string>(
                    sample.Parameters,
                    StringComparer.OrdinalIgnoreCase)
            })
            .ToList();

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
