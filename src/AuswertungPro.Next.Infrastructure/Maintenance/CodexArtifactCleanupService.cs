using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Maintenance;

namespace AuswertungPro.Next.Infrastructure.Maintenance;

/// <summary>
/// Entfernt nur alte Codex-Baukopien, deren oberste Ebene ausschliesslich
/// aus bin-, obj- oder TestResults-Ordnern besteht.
/// </summary>
public sealed class CodexArtifactCleanupService : ICodexArtifactCleanupService
{
    public const string ArtifactDirectoryName = ".codex-artifacts";
    private readonly CodexArtifactCandidateInspector _inspector = new();

    private static readonly EnumerationOptions RecursiveFiles = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public CodexArtifactCleanupReport Analyze(CodexArtifactCleanupRequest request)
    {
        var context = CleanupContext.Create(request);
        var warnings = new List<string>();
        var items = new List<CodexArtifactCleanupItem>();

        if (!Directory.Exists(context.ArtifactRoot))
            return BuildReport(context, items, warnings);

        if (CodexArtifactCandidateInspector.IsReparsePoint(context.ArtifactRoot))
        {
            warnings.Add("Der Ordner .codex-artifacts ist eine Verknuepfung und bleibt geschuetzt.");
            return BuildReport(context, items, warnings);
        }

        string[] candidates;
        try
        {
            candidates = Directory.GetDirectories(context.ArtifactRoot, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            warnings.Add($"Der Ordner .codex-artifacts konnte nicht gelesen werden: {ex.Message}");
            return BuildReport(context, items, warnings);
        }

        foreach (var candidate in candidates)
        {
            var inspection = _inspector.Inspect(
                candidate,
                context.ArtifactRoot,
                context.ActivityCutoffUtc);
            if (!inspection.CanDelete)
            {
                if (!string.IsNullOrWhiteSpace(inspection.Warning))
                    warnings.Add(inspection.Warning);
                continue;
            }

            items.Add(new CodexArtifactCleanupItem(
                Path.GetFullPath(candidate),
                inspection.SizeBytes,
                inspection.FileCount,
                inspection.LatestWriteUtc));
        }

        return BuildReport(context, items, warnings);
    }

    public CodexArtifactCleanupResult Clean(
        CodexArtifactCleanupRequest request,
        IReadOnlyCollection<string> approvedPaths)
    {
        ArgumentNullException.ThrowIfNull(approvedPaths);
        var context = CleanupContext.Create(request);
        var failures = new List<string>();
        long freedBytes = 0;
        var deletedFiles = 0;
        var deletedDirectories = 0;

        foreach (var approvedPath in approvedPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var originalPath = approvedPath;
            string? quarantinePath = null;
            try
            {
                if (!Directory.Exists(originalPath))
                    continue;

                // Nur die zuvor angezeigten Pfade duerfen entfernt werden. Jeder davon
                // wird unmittelbar vor dem Verschieben noch einmal vollstaendig geprueft.
                var inspection = _inspector.Inspect(
                    originalPath,
                    context.ArtifactRoot,
                    context.ActivityCutoffUtc);
                if (!inspection.CanDelete)
                {
                    failures.Add($"{originalPath}: Sicherheitspruefung vor dem Loeschen fehlgeschlagen.");
                    continue;
                }

                quarantinePath = Path.Combine(
                    context.ArtifactRoot,
                    $".cleanup-{Path.GetFileName(originalPath)}-{Guid.NewGuid():N}");
                Directory.Move(originalPath, quarantinePath);
                ClearReadOnlyAttributes(quarantinePath);
                Directory.Delete(quarantinePath, recursive: true);
                freedBytes += inspection.SizeBytes;
                deletedFiles += inspection.FileCount;
                deletedDirectories++;
            }
            catch (Exception ex)
            {
                var remainingPath = quarantinePath is not null && Directory.Exists(quarantinePath)
                    ? quarantinePath
                    : originalPath;
                var remaining = _inspector.MeasureRemainingBytes(remainingPath);

                if (quarantinePath is not null
                    && Directory.Exists(quarantinePath)
                    && !Directory.Exists(originalPath))
                {
                    try { Directory.Move(quarantinePath, originalPath); }
                    catch { /* Der Fehler wird unten samt Originalpfad gemeldet. */ }
                }

                failures.Add($"{originalPath}: {ex.Message} (verblieben: {remaining:N0} Bytes)");
            }
        }

        return new CodexArtifactCleanupResult(
            freedBytes,
            deletedFiles,
            deletedDirectories,
            failures);
    }

    private static CodexArtifactCleanupReport BuildReport(
        CleanupContext context,
        IEnumerable<CodexArtifactCleanupItem> items,
        IReadOnlyList<string> warnings)
        => new(
            context.ArtifactRoot,
            context.ActivityCutoffUtc,
            items.OrderByDescending(item => item.SizeBytes)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            warnings);

    private static void ClearReadOnlyAttributes(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", RecursiveFiles))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* Der folgende Loeschversuch liefert den konkreten Fehler. */ }
        }
    }

    private sealed record CleanupContext(string ArtifactRoot, DateTime ActivityCutoffUtc)
    {
        public static CleanupContext Create(CodexArtifactCleanupRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.ProgramRoot))
                throw new ArgumentException("Programmordner fehlt.", nameof(request));

            var programRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.ProgramRoot));
            if (!Directory.Exists(programRoot))
                throw new DirectoryNotFoundException($"Programmordner nicht gefunden: {programRoot}");

            return new CleanupContext(
                Path.Combine(programRoot, ArtifactDirectoryName),
                request.ActivityCutoffUtc);
        }
    }

}
