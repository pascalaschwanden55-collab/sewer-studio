using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public static class TrainingSamplesReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<TrainingSampleDto> List(string knowledgeRoot, string caseId)
    {
        if (string.IsNullOrWhiteSpace(knowledgeRoot) || string.IsNullOrWhiteSpace(caseId))
            return [];

        var path = Path.Combine(knowledgeRoot, "training_samples.json");
        if (!File.Exists(path))
            return [];

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var samples = JsonSerializer.Deserialize<List<TrainingSample>>(stream, JsonOptions) ?? [];

        return samples
            .Where(s => string.Equals(s.CaseId, caseId, StringComparison.OrdinalIgnoreCase))
            .Select(s => new TrainingSampleDto(
                SampleId: s.SampleId,
                CaseId: s.CaseId,
                Code: s.Code,
                Beschreibung: s.Beschreibung,
                MeterStart: s.MeterStart,
                MeterEnd: s.MeterEnd,
                IsStreckenschaden: s.IsStreckenschaden,
                TimeSeconds: s.TimeSeconds,
                FramePath: s.FramePath,
                Status: s.Status.ToString(),
                MatchLevel: s.MatchLevel,
                KiCode: s.KiCode,
                SourceType: s.SourceType,
                KbIndexState: s.KbIndexState.ToString()))
            .ToArray();
    }
}
