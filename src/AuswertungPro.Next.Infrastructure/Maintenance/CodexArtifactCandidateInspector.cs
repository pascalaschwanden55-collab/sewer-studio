using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Maintenance;

/// <summary>
/// Entscheidet ohne Loeschzugriff, ob eine Codex-Baukopie sicher entbehrlich ist.
/// </summary>
internal sealed class CodexArtifactCandidateInspector
{
    private static readonly HashSet<string> AllowedContentDirectoryNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults"
    };

    public CodexArtifactCandidateInspection Inspect(
        string path,
        string artifactRoot,
        DateTime activityCutoffUtc)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return CodexArtifactCandidateInspection.Reject(
                $"Ungueltiger Artefakt-Pfad wurde uebersprungen: {ex.Message}");
        }

        var displayName = Path.GetFileName(fullPath);
        if (!IsDirectChild(fullPath, artifactRoot))
        {
            return CodexArtifactCandidateInspection.Reject(
                $"Artefakt ausserhalb des Schutzordners wurde uebersprungen: {fullPath}");
        }

        if (IsReparsePoint(fullPath))
        {
            return CodexArtifactCandidateInspection.Reject(
                $"Verknuepfter Artefakt-Ordner wurde uebersprungen: {displayName}");
        }

        try
        {
            var topEntries = Directory.GetFileSystemEntries(fullPath, "*", SearchOption.TopDirectoryOnly);
            if (topEntries.Length == 0)
                return CodexArtifactCandidateInspection.Reject();

            foreach (var entry in topEntries)
            {
                var attributes = File.GetAttributes(entry);
                var name = Path.GetFileName(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0
                    || (attributes & FileAttributes.Directory) == 0
                    || !AllowedContentDirectoryNames.Contains(name))
                {
                    return CodexArtifactCandidateInspection.Reject(
                        $"Artefakt mit unbekanntem Inhalt bleibt geschuetzt: {displayName}");
                }
            }

            var measured = MeasureDirectory(fullPath);
            if (!measured.Complete)
            {
                return CodexArtifactCandidateInspection.Reject(
                    $"Nicht vollstaendig pruefbares Artefakt bleibt geschuetzt: {displayName}");
            }

            if (measured.ContainsReparsePoint)
            {
                return CodexArtifactCandidateInspection.Reject(
                    $"Artefakt mit Verknuepfung bleibt geschuetzt: {displayName}");
            }

            if (measured.ContainsProjectMarker)
            {
                return CodexArtifactCandidateInspection.Reject(
                    $"Artefakt mit Projektdateien bleibt geschuetzt: {displayName}");
            }

            if (measured.LatestWriteUtc >= activityCutoffUtc)
            {
                return CodexArtifactCandidateInspection.Reject(
                    $"Kuerzlich verwendetes Artefakt bleibt geschuetzt: {displayName}");
            }

            return CodexArtifactCandidateInspection.Accept(
                measured.SizeBytes,
                measured.FileCount,
                measured.LatestWriteUtc);
        }
        catch (Exception ex)
        {
            return CodexArtifactCandidateInspection.Reject(
                $"Artefakt konnte nicht sicher geprueft werden und bleibt erhalten: {displayName} ({ex.Message})");
        }
    }

    public long MeasureRemainingBytes(string path)
    {
        try { return Directory.Exists(path) ? MeasureDirectory(path).SizeBytes : 0; }
        catch { return 0; }
    }

    public static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; }
    }

    private static CodexArtifactDirectoryMeasure MeasureDirectory(string path)
    {
        long sizeBytes = 0;
        var fileCount = 0;
        var latestWriteUtc = Directory.GetLastWriteTimeUtc(path);
        var containsReparsePoint = false;
        var containsProjectMarker = false;
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(current, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return CodexArtifactDirectoryMeasure.Incomplete;
            }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch
                {
                    return CodexArtifactDirectoryMeasure.Incomplete;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    containsReparsePoint = true;
                    continue;
                }

                var name = Path.GetFileName(entry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (name.Equals("Projektdateien", StringComparison.OrdinalIgnoreCase))
                        containsProjectMarker = true;

                    latestWriteUtc = Max(latestWriteUtc, Directory.GetLastWriteTimeUtc(entry));
                    pending.Push(entry);
                    continue;
                }

                if (name.Equals("projekt.json", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("projekt.pointer", StringComparison.OrdinalIgnoreCase))
                {
                    containsProjectMarker = true;
                }

                var info = new FileInfo(entry);
                sizeBytes += info.Length;
                fileCount++;
                latestWriteUtc = Max(latestWriteUtc, info.LastWriteTimeUtc);
            }
        }

        return new CodexArtifactDirectoryMeasure(
            sizeBytes,
            fileCount,
            latestWriteUtc,
            containsReparsePoint,
            containsProjectMarker,
            true);
    }

    private static bool IsDirectChild(string path, string root)
        => string.Equals(
            Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)),
            Path.TrimEndingDirectorySeparator(root),
            StringComparison.OrdinalIgnoreCase);

    private static DateTime Max(DateTime left, DateTime right) => left >= right ? left : right;
}

internal sealed record CodexArtifactCandidateInspection(
    bool CanDelete,
    long SizeBytes,
    int FileCount,
    DateTime LatestWriteUtc,
    string? Warning)
{
    public static CodexArtifactCandidateInspection Accept(
        long sizeBytes,
        int fileCount,
        DateTime latestWriteUtc)
        => new(true, sizeBytes, fileCount, latestWriteUtc, null);

    public static CodexArtifactCandidateInspection Reject(string? warning = null)
        => new(false, 0, 0, DateTime.MinValue, warning);
}

internal sealed record CodexArtifactDirectoryMeasure(
    long SizeBytes,
    int FileCount,
    DateTime LatestWriteUtc,
    bool ContainsReparsePoint,
    bool ContainsProjectMarker,
    bool Complete)
{
    public static CodexArtifactDirectoryMeasure Incomplete { get; } =
        new(0, 0, DateTime.MinValue, false, false, false);
}
