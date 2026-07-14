using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Verhindert, dass optionale Ablaufdaten die Videoanalyse abbrechen.</summary>
internal static class PipelineTraceWriteGuard
{
    public static Task WriteAsync(IPipelineTraceWriter writer, PipelineTraceEntry entry)
        => BestEffort.TryAsync(
            () => writer.WriteAsync(entry),
            $"PipelineTraceWriter Trace schreiben: {entry.RunId}");

    public static Task WriteSummaryAsync(
        IPipelineTraceWriter writer,
        string runId,
        TelemetrySummary summary)
        => BestEffort.TryAsync(
            () => writer.WriteSummaryAsync(runId, summary),
            $"PipelineTraceWriter Summary schreiben: {runId}");

    public static string? ResolvePath(IPipelineTraceWriter writer, string runId)
    {
        string? path = null;
        BestEffort.Try(
            () => path = writer.ResolvePath(runId),
            $"PipelineTraceWriter Trace-Pfad aufloesen: {runId}");
        return path;
    }

    public static string? ResolveSummaryPath(IPipelineTraceWriter writer, string runId)
    {
        string? path = null;
        BestEffort.Try(
            () => path = writer.ResolveSummaryPath(runId),
            $"PipelineTraceWriter Summary-Pfad aufloesen: {runId}");
        return path;
    }
}
