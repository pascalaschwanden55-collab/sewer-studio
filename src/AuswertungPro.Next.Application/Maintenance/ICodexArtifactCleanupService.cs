using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Maintenance;

public sealed record CodexArtifactCleanupRequest(
    string ProgramRoot,
    DateTime ActivityCutoffUtc);

public sealed record CodexArtifactCleanupItem(
    string Path,
    long SizeBytes,
    int FileCount,
    DateTime LatestWriteUtc);

public sealed record CodexArtifactCleanupReport(
    string ArtifactRoot,
    DateTime ActivityCutoffUtc,
    IReadOnlyList<CodexArtifactCleanupItem> Items,
    IReadOnlyList<string> ScanWarnings)
{
    public long TotalBytes => Items.Sum(item => item.SizeBytes);
    public int TotalFiles => Items.Sum(item => item.FileCount);
}

public sealed record CodexArtifactCleanupResult(
    long FreedBytes,
    int DeletedFiles,
    int DeletedDirectories,
    IReadOnlyList<string> FailedPaths)
{
    public bool Success => FailedPaths.Count == 0;
}

/// <summary>
/// Prueft und entfernt alte, rein erzeugte Baukopien aus .codex-artifacts.
/// </summary>
public interface ICodexArtifactCleanupService
{
    CodexArtifactCleanupReport Analyze(CodexArtifactCleanupRequest request);

    CodexArtifactCleanupResult Clean(
        CodexArtifactCleanupRequest request,
        IReadOnlyCollection<string> approvedPaths);
}
