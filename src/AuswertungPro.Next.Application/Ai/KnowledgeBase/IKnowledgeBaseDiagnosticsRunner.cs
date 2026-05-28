using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

public interface IKnowledgeBaseDiagnosticsRunner
{
    Task<KnowledgeBaseStatusReport> ReadStatusAsync(int topCodes = 20, CancellationToken ct = default);
    Task<KnowledgeBaseQualityReport> ReadQualityAsync(CancellationToken ct = default);
    Task<KnowledgeBaseDiagnosticsSummary> ReadSummaryAsync(int topCodes = 12, CancellationToken ct = default);
}

public sealed record KnowledgeBaseDiagnosticsCodeCount(string VsaCode, int Count);

public sealed record KnowledgeBaseDiagnosticsSummary(
    int SampleCount,
    int EmbeddingCount,
    int VersionCount,
    DateTimeOffset? LatestVersionAtUtc,
    int LatestVersionSampleCount,
    string LatestVersionNotes,
    IReadOnlyList<KnowledgeBaseDiagnosticsCodeCount> TopCodes);

public sealed record KnowledgeBaseStatusReport(
    int SampleCount,
    int ErrorCount,
    int NewCount,
    int EmbeddingCount,
    int CodesCovered,
    DateTimeOffset? LatestVersionAtUtc,
    IReadOnlyList<KnowledgeBaseDiagnosticsCodeCount> TopCodes);

public sealed record KnowledgeBaseQualityReport(
    string CoverageGapsText,
    int CoverageGapsCount,
    string AccuracyText,
    int StaleSampleCount);
