using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace SidecarE2eSmoke;

public static class SidecarSmokeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed record HealthReport(
    bool IsReachable,
    bool IsAuthorized,
    int? StatusCode,
    string? Error,
    string? Status,
    string? Version,
    double? VramAllocatedGb,
    double? VramTotalGb,
    IReadOnlyList<string> LoadedModels);

public sealed record SmokeCheckReport(string Name, bool Passed, string Detail);

public sealed record ExtractedFrame(int Index, double TimestampSec, byte[] Bytes);

public sealed record FramePipelineReport(
    int Index,
    double TimestampSec,
    int ImageBytes,
    bool IsRelevant,
    int DinoDetections,
    int SamMasks,
    int QuantifiedMasks,
    double TotalTimeMs,
    string? Error);

public sealed class SidecarSmokeReport
{
    public DateTimeOffset CreatedUtc { get; init; }
    public string SidecarUrl { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool FullPipeline { get; init; }
    public bool SidecarStartedByTool { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public HealthReport? Health { get; set; }
    public YoloClassifyResponse? Classify { get; set; }
    public YoloResponse? Yolo { get; set; }
    public DinoResponse? Dino { get; set; }
    public SamResponse? Sam { get; set; }
    public string? SamSkippedReason { get; set; }
    public IReadOnlyList<MaskQuantificationService.QuantifiedMask> QuantifiedMasks { get; set; } =
        Array.Empty<MaskQuantificationService.QuantifiedMask>();
    public IReadOnlyList<FramePipelineReport> Frames { get; set; } = Array.Empty<FramePipelineReport>();
    public List<SmokeCheckReport> Checks { get; } = [];
    public GoldenValidationReport? GoldenValidation { get; set; }

    public void AddCheck(string name, bool passed, string detail)
    {
        var old = Checks.FindIndex(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        var check = new SmokeCheckReport(name, passed, detail);
        if (old >= 0)
            Checks[old] = check;
        else
            Checks.Add(check);

        Console.WriteLine($"{(passed ? "OK" : "FEHLER"),-6} {name}: {detail}");
    }

    public static SidecarSmokeReport Failed(SidecarSmokeOptions options, string error) => new()
    {
        CreatedUtc = DateTimeOffset.UtcNow,
        SidecarUrl = options.SidecarUrl,
        Source = options.SourceDescription,
        FullPipeline = options.FullPipeline,
        Success = false,
        Error = error,
    };
}

public sealed class PipelineGoldenContract
{
    public int ContractVersion { get; init; }
    public string ExpectedSidecarVersion { get; init; } = string.Empty;
    public int MinimumDecodedFrames { get; init; }
    public int MinimumSamMasks { get; init; }
    public IReadOnlyList<string> RequiredChecks { get; init; } = Array.Empty<string>();

    public static PipelineGoldenContract Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PipelineGoldenContract>(json, SidecarSmokeJson.Options)
               ?? throw new InvalidDataException($"Golden-Vertrag ist leer oder ungueltig: {path}");
    }
}

public sealed record GoldenValidationReport(
    bool Success,
    string ContractPath,
    IReadOnlyList<string> Failures);

public static class GoldenContractValidator
{
    public static GoldenValidationReport Validate(
        SidecarSmokeReport report,
        PipelineGoldenContract contract,
        string contractPath)
    {
        var failures = new List<string>();
        if (contract.ContractVersion != 1)
            failures.Add($"Unbekannte Vertragsversion: {contract.ContractVersion}");

        if (!string.Equals(
                report.Health?.Version,
                contract.ExpectedSidecarVersion,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"Sidecar-Version: erwartet {contract.ExpectedSidecarVersion}, erhalten {report.Health?.Version ?? "(leer)"}");
        }

        if (report.Frames.Count < contract.MinimumDecodedFrames)
            failures.Add($"Videobilder: mindestens {contract.MinimumDecodedFrames}, erhalten {report.Frames.Count}");

        var samMasks = report.Sam?.Masks.Count ?? 0;
        if (samMasks < contract.MinimumSamMasks)
            failures.Add($"SAM-Masken: mindestens {contract.MinimumSamMasks}, erhalten {samMasks}");

        var checks = report.Checks.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var required in contract.RequiredChecks)
        {
            if (!checks.TryGetValue(required, out var actual))
                failures.Add($"Pflichtpruefung fehlt: {required}");
            else if (!actual.Passed)
                failures.Add($"Pflichtpruefung fehlgeschlagen: {required} ({actual.Detail})");
        }

        return new GoldenValidationReport(failures.Count == 0, contractPath, failures);
    }
}
