namespace AuswertungPro.Next.Application.Ai;

/// <summary>Schreibt Laufzeitdaten der lokalen Vision-Verarbeitung.</summary>
public interface ISidecarTelemetryWriter
{
    Task WriteAsync(SidecarTelemetryEntry entry);

    string? ResolvePath();
}

public record SidecarTelemetryEntry(
    DateTimeOffset TimestampUtc,
    string Endpoint,
    string? ModelName,
    long RoundtripMs,
    double InferenceTimeMs,
    double QueueWaitMs,
    string? Device,
    double? VramAllocatedGb,
    double? VramTotalGb,
    int DetectionCount,
    bool? IsRelevant,
    string? FrameClass);
