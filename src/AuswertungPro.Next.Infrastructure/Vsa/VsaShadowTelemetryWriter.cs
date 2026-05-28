using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Vsa;

public static class VsaShadowTelemetryWriter
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static void Write(VsaShadowTelemetryEvent entry, string? pathOverride = null)
    {
        try
        {
            var path = pathOverride ?? ResolvePath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

            lock (Sync)
            {
                File.AppendAllText(path, line);
            }
        }
        catch
        {
            // Shadow telemetry must never change the productive VSA result.
        }
    }

    public static string? ResolvePath()
    {
        var overrideDir = Environment.GetEnvironmentVariable("SEWERSTUDIO_TELEMETRY_DIR");
        var root = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return string.IsNullOrWhiteSpace(root)
            ? null
            : Path.Combine(root, "SewerStudio", "Telemetry", "vsa_shadow.jsonl");
    }
}

public sealed record VsaShadowTelemetryEvent(
    DateTimeOffset TimestampUtc,
    string Code,
    string BaseCode,
    string Requirement,
    int? LegacyEz,
    int? V2Ez,
    bool ExpectedDrift,
    string? V2Reason = null,
    string? Ch1 = null,
    string? Ch2 = null,
    string? Q1 = null,
    string? Q2 = null,
    string? Material = null,
    string? Dn = null,
    string? V2RuleId = null,
    string? V2SourceRef = null);
