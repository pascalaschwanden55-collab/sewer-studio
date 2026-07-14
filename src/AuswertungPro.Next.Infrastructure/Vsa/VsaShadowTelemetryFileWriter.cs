using System.Text.Json;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Infrastructure.Telemetry;

namespace AuswertungPro.Next.Infrastructure.Vsa;

/// <summary>Dateibasierte Best-Effort-Ausgabe der VSA-Schattenauswertung.</summary>
public sealed class VsaShadowTelemetryFileWriter : IVsaShadowTelemetryWriter
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public void Write(VsaShadowTelemetryEntry entry, string? pathOverride = null)
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
            // Schatten-Telemetrie darf das produktive VSA-Ergebnis nie veraendern.
        }
    }

    public string? ResolvePath()
        => TelemetryPathResolver.ResolveFile("vsa_shadow.jsonl");
}
