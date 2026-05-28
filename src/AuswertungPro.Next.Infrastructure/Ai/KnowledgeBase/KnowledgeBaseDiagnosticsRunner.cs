using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Monitoring;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public sealed class KnowledgeBaseDiagnosticsRunner(string? dbPath = null) : IKnowledgeBaseDiagnosticsRunner
{
    public async Task<KnowledgeBaseStatusReport> ReadStatusAsync(int topCodes = 20, CancellationToken ct = default)
    {
        var (summary, distinctCodeCount) = await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var db = CreateContext();
            var diagnostics = new KnowledgeBaseDiagnosticsService(db);
            var statusSummary = MapSummary(diagnostics.ReadSummary(topCodes));
            var codeCount = diagnostics.ReadAllCodeCounts().Count;
            return (statusSummary, codeCount);
        }, ct).ConfigureAwait(false);

        var errorCount = 0;
        var newCount = 0;
        try
        {
            var samples = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
            foreach (var sample in samples)
            {
                if (sample.KbIndexState == KbIndexState.Error)
                    errorCount++;
                else if (sample.Status == TrainingSampleStatus.New)
                    newCount++;
            }
        }
        catch
        {
            // Optional JSON status file can be missing or corrupt; DB status stays useful.
        }

        return new KnowledgeBaseStatusReport(
            summary.SampleCount,
            errorCount,
            newCount,
            summary.EmbeddingCount,
            distinctCodeCount,
            summary.LatestVersionAtUtc,
            summary.TopCodes);
    }

    public Task<KnowledgeBaseQualityReport> ReadQualityAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var path = dbPath ?? KnowledgeBaseContext.DefaultDbPath;
            if (!File.Exists(path))
            {
                return new KnowledgeBaseQualityReport(
                    "KB noch nicht erstellt",
                    0,
                    "Noch keine Validierungsdaten",
                    0);
            }

            using var db = CreateContext();
            var diagnostics = new KnowledgeBaseDiagnosticsService(db);

            var allCodes = diagnostics.ReadAllCodeCounts();
            var underRepresented = allCodes.Where(c => c.Count < 3).ToList();
            var gapsText = allCodes.Count == 0
                ? "KB leer - noch keine Samples indexiert"
                : underRepresented.Count > 0
                    ? string.Join("\n", underRepresented.Select(c => $"{c.VsaCode}: {c.Count} Samples"))
                    : "Keine Luecken (alle Codes >= 3 Samples)";

            var accuracyText = ReadAccuracyText(db);
            var staleCount = ReadStaleSampleCount(db);

            return new KnowledgeBaseQualityReport(
                gapsText,
                underRepresented.Count,
                accuracyText,
                staleCount);
        }, ct);
    }

    public Task<KnowledgeBaseDiagnosticsSummary> ReadSummaryAsync(int topCodes = 12, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var db = CreateContext();
            var diagnostics = new KnowledgeBaseDiagnosticsService(db);
            return MapSummary(diagnostics.ReadSummary(topCodes));
        }, ct);
    }

    private KnowledgeBaseContext CreateContext() => new(dbPath);

    private static string ReadAccuracyText(KnowledgeBaseContext db)
    {
        try
        {
            var accuracy = new AccuracyDashboardService(db.Connection);
            var metrics = accuracy.ComputeMetrics();
            return metrics.Count > 0
                ? string.Join("\n", metrics
                    .OrderByDescending(m => m.TruePositives + m.FalsePositives + m.FalseNegatives)
                    .Take(8)
                    .Select(m =>
                        $"{m.VsaCode}: F1={m.F1Score:F2}  P={m.Precision:F2}  R={m.Recall:F2}  (n={m.TruePositives + m.FalsePositives + m.FalseNegatives})"))
                : "Noch keine Validierungsdaten";
        }
        catch
        {
            return "Validierungsdaten nicht verfuegbar";
        }
    }

    private static int ReadStaleSampleCount(KnowledgeBaseContext db)
    {
        try
        {
            var quality = new KbQualityService(db.Connection);
            return quality.FindStaleCandidates().Count;
        }
        catch
        {
            return 0;
        }
    }

    private static KnowledgeBaseDiagnosticsSummary MapSummary(KnowledgeBaseSummary summary)
        => new(
            summary.SampleCount,
            summary.EmbeddingCount,
            summary.VersionCount,
            summary.LatestVersionAtUtc,
            summary.LatestVersionSampleCount,
            summary.LatestVersionNotes,
            summary.TopCodes
                .Select(c => new KnowledgeBaseDiagnosticsCodeCount(c.VsaCode, c.Count))
                .ToList());
}
