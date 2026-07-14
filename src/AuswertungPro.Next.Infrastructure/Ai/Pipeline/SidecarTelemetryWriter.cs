using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Kompatibilitaetsfassade; die Dateiarbeit liegt im Instanzdienst.</summary>
public static class SidecarTelemetryWriter
{
    private static ISidecarTelemetryWriter _current = new SidecarTelemetryFileWriter();

    public static ISidecarTelemetryWriter Current => Volatile.Read(ref _current);

    public static void Use(ISidecarTelemetryWriter writer) =>
        Volatile.Write(ref _current, writer ?? throw new ArgumentNullException(nameof(writer)));

    public static Task WriteAsync(SidecarTelemetryEvent entry) => Current.WriteAsync(entry);

    public static string? ResolvePath() => Current.ResolvePath();
}

public sealed record SidecarTelemetryEvent(
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
    string? FrameClass)
    : SidecarTelemetryEntry(
        TimestampUtc,
        Endpoint,
        ModelName,
        RoundtripMs,
        InferenceTimeMs,
        QueueWaitMs,
        Device,
        VramAllocatedGb,
        VramTotalGb,
        DetectionCount,
        IsRelevant,
        FrameClass);
