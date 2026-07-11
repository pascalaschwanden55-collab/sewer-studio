using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Maintenance;

public enum ProgramCleanupCategory
{
    WorkspaceTemp,
    BuildOutput,
    PythonCache,
    SystemTemp
}

public enum ProgramCleanupItemType
{
    Directory,
    File
}

public sealed record ProgramCleanupRequest(
    string ProgramRoot,
    string SystemTempRoot,
    string CurrentAppBaseDirectory,
    IReadOnlyCollection<string>? ProtectedProjectRoots = null,
    DateTime? TemporaryFileCutoffUtc = null);

public sealed record ProgramCleanupItem(
    string Path,
    ProgramCleanupCategory Category,
    ProgramCleanupItemType ItemType,
    long SizeBytes,
    int FileCount);

public sealed record ProgramCleanupReport(
    string ProgramRoot,
    IReadOnlyList<ProgramCleanupItem> Items,
    IReadOnlyList<string> ScanWarnings)
{
    public long TotalBytes => Items.Sum(item => item.SizeBytes);
    public int TotalFiles => Items.Sum(item => item.FileCount);
}

public sealed record ProgramCleanupResult(
    long FreedBytes,
    int DeletedFiles,
    int DeletedDirectories,
    IReadOnlyList<string> FailedPaths)
{
    public bool Success => FailedPaths.Count == 0;
}

/// <summary>
/// Entfernt ausschliesslich bekannte, jederzeit neu erzeugbare SewerStudio-Daten.
/// Projekt-, Modell-, Karten-, Trainings-, Git- und Release-Ordner stehen nie auf der Loeschliste.
/// </summary>
public sealed class ProgramCleanupService
{
    private static readonly HashSet<string> BuildDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "TestResults"
    };

    private static readonly HashSet<string> PythonCacheDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "__pycache__",
        ".pytest_cache",
        ".pytest_tmp",
        ".ruff_cache",
        ".mypy_cache",
        ".uv-cache"
    };

    private static readonly HashSet<string> TraversalRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "src",
        "tests",
        "tools",
        "sidecar",
        "training",
        "integrations",
        ".worktrees"
    };

    private static readonly HashSet<string> TraversalSkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".venv",
        "models",
        "training_export",
        "node_modules",
        "artifacts",
        "basemap_tiles"
    };

    private static readonly string[] SewerStudioTempFilePatterns =
    {
        "sewer_live_*.png",
        "sewer_studio_det_*.png",
        "auswertungpro_frame_*.png",
        "pdfcorr_*.pdf",
        "mdb_dump_*.ps1",
        "mdb_dump_*.json",
        "pdf_extract_*.txt",
        "pdf_ocr_*",
        "sewer-sidecar-e2e-*.jpg"
    };

    private static readonly EnumerationOptions RecursiveFiles = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public ProgramCleanupReport Analyze(ProgramCleanupRequest request)
    {
        var context = CleanupContext.Create(request);
        var items = new List<ProgramCleanupItem>();
        var warnings = new List<string>();

        DiscoverWorkspaceTempDirectories(context, items, warnings);
        DiscoverGeneratedDirectories(context, items, warnings);
        DiscoverSystemTemp(context, items, warnings);

        var distinctItems = items
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.SizeBytes)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ProgramCleanupReport(context.ProgramRoot, distinctItems, warnings);
    }

    public ProgramCleanupResult Clean(ProgramCleanupRequest request)
    {
        // Absichtlich neu analysieren: Geloescht werden nur aktuell noch vorhandene
        // Pfade, die auch jetzt alle Whitelist- und Schutzregeln bestehen.
        var report = Analyze(request);
        var failures = new List<string>();
        long freedBytes = 0;
        var deletedFiles = 0;
        var deletedDirectories = 0;

        foreach (var item in report.Items)
        {
            try
            {
                if (item.ItemType == ProgramCleanupItemType.File)
                {
                    if (!File.Exists(item.Path))
                        continue;

                    File.SetAttributes(item.Path, FileAttributes.Normal);
                    File.Delete(item.Path);
                    freedBytes += item.SizeBytes;
                    deletedFiles++;
                    continue;
                }

                if (!Directory.Exists(item.Path))
                    continue;

                var markerScan = ScanForProjectMarker(item.Path);
                if (IsReparsePoint(item.Path) || markerScan != ProjectMarkerScanResult.NotFound)
                {
                    failures.Add($"{item.Path}: Sicherheitspruefung vor dem Loeschen fehlgeschlagen.");
                    continue;
                }

                ClearReadOnlyAttributes(item.Path);
                Directory.Delete(item.Path, recursive: true);
                freedBytes += item.SizeBytes;
                deletedFiles += item.FileCount;
                deletedDirectories++;
            }
            catch (Exception ex)
            {
                var remaining = item.ItemType == ProgramCleanupItemType.File
                    ? TryGetFileSize(item.Path)
                    : TryMeasureDirectory(item.Path).SizeBytes;
                freedBytes += Math.Max(0, item.SizeBytes - remaining);
                failures.Add($"{item.Path}: {ex.Message}");
            }
        }

        return new ProgramCleanupResult(freedBytes, deletedFiles, deletedDirectories, failures);
    }

    private static void DiscoverWorkspaceTempDirectories(
        CleanupContext context,
        ICollection<ProgramCleanupItem> items,
        ICollection<string> warnings)
    {
        foreach (var path in EnumerateDirectoriesSafe(context.ProgramRoot))
        {
            var name = Path.GetFileName(path);
            if (!IsWorkspaceTempName(name))
                continue;

            AddDirectoryCandidate(
                path,
                ProgramCleanupCategory.WorkspaceTemp,
                context,
                items,
                warnings);
        }
    }

    private static void DiscoverGeneratedDirectories(
        CleanupContext context,
        ICollection<ProgramCleanupItem> items,
        ICollection<string> warnings)
    {
        foreach (var rootName in TraversalRootNames)
        {
            var root = Path.Combine(context.ProgramRoot, rootName);
            if (!Directory.Exists(root) || IsReparsePoint(root))
                continue;

            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var child in EnumerateDirectoriesSafe(current))
                {
                    if (IsReparsePoint(child) || context.IsProtectedByProject(child))
                        continue;

                    var name = Path.GetFileName(child);
                    if (BuildDirectoryNames.Contains(name))
                    {
                        AddDirectoryCandidate(
                            child,
                            ProgramCleanupCategory.BuildOutput,
                            context,
                            items,
                            warnings);
                        continue;
                    }

                    if (PythonCacheDirectoryNames.Contains(name))
                    {
                        AddDirectoryCandidate(
                            child,
                            ProgramCleanupCategory.PythonCache,
                            context,
                            items,
                            warnings);
                        continue;
                    }

                    if (!TraversalSkipNames.Contains(name))
                        pending.Push(child);
                }
            }
        }
    }

    private static void DiscoverSystemTemp(
        CleanupContext context,
        ICollection<ProgramCleanupItem> items,
        ICollection<string> warnings)
    {
        if (!Directory.Exists(context.SystemTempRoot))
            return;

        foreach (var pattern in SewerStudioTempFilePatterns)
        {
            foreach (var path in EnumerateFilesSafe(context.SystemTempRoot, pattern, SearchOption.TopDirectoryOnly))
                AddOldTempFile(path, context, items);
        }

        // Nur gerenderte Vorschauen sind sicher entbehrlich. coding_ai_frames kann
        // dagegen noch von einem ungespeicherten Projekt als Beweisbild referenziert sein.
        var previewRoot = Path.Combine(context.SystemTempRoot, "SewerStudio", "coding_defect_previews");
        if (Directory.Exists(previewRoot) && !IsReparsePoint(previewRoot))
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(previewRoot, "*", RecursiveFiles))
                    AddOldTempFile(path, context, items);
            }
            catch (Exception ex)
            {
                warnings.Add($"Temp-Ordner nicht vollstaendig gelesen: {ex.Message}");
            }
        }

        // sewerstudio_import_backup_* bleibt absichtlich erhalten: Nach einem
        // fehlgeschlagenen KI-Wissen-Import kann dieser Ordner die letzte Rettung sein.
    }

    private static void AddOldTempFile(
        string path,
        CleanupContext context,
        ICollection<ProgramCleanupItem> items)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!IsDescendant(fullPath, context.SystemTempRoot)
                || File.GetLastWriteTimeUtc(fullPath) >= context.TempCutoffUtc)
            {
                return;
            }

            items.Add(new ProgramCleanupItem(
                fullPath,
                ProgramCleanupCategory.SystemTemp,
                ProgramCleanupItemType.File,
                new FileInfo(fullPath).Length,
                1));
        }
        catch
        {
            // Eine gerade verschwindende Temp-Datei ist kein Analysefehler.
        }
    }

    private static void AddDirectoryCandidate(
        string path,
        ProgramCleanupCategory category,
        CleanupContext context,
        ICollection<ProgramCleanupItem> items,
        ICollection<string> warnings)
    {
        var fullPath = Path.GetFullPath(path);
        if (!IsDescendant(fullPath, context.ProgramRoot)
            || context.IsProtectedByProject(fullPath)
            || IsReparsePoint(fullPath))
        {
            return;
        }

        if (PathsEqual(fullPath, context.CurrentAppBaseDirectory))
        {
            warnings.Add($"Laufender Programmordner uebersprungen: {fullPath}");
            return;
        }

        if (IsSameOrAncestor(fullPath, context.CurrentAppBaseDirectory))
        {
            foreach (var child in EnumerateDirectoriesSafe(fullPath))
                AddDirectoryCandidate(child, category, context, items, warnings);
            return;
        }

        var markerScan = ScanForProjectMarker(fullPath);
        if (markerScan == ProjectMarkerScanResult.Found)
        {
            warnings.Add($"Moeglicher Projektordner wurde uebersprungen: {fullPath}");
            return;
        }
        if (markerScan == ProjectMarkerScanResult.Incomplete)
        {
            warnings.Add($"Nicht vollstaendig pruefbarer Ordner wurde uebersprungen: {fullPath}");
            return;
        }

        var measured = TryMeasureDirectory(fullPath);
        if (measured.ContainsReparsePoint)
        {
            warnings.Add($"Verknuepfter Ordner wurde zur Sicherheit uebersprungen: {fullPath}");
            return;
        }

        items.Add(new ProgramCleanupItem(
            fullPath,
            category,
            ProgramCleanupItemType.Directory,
            measured.SizeBytes,
            measured.FileCount));
    }

    private static DirectoryMeasure TryMeasureDirectory(string path)
    {
        if (!Directory.Exists(path))
            return new DirectoryMeasure(0, 0, DateTime.MinValue, false);

        long size = 0;
        var files = 0;
        DateTime latestWrite;
        try { latestWrite = Directory.GetLastWriteTimeUtc(path); }
        catch { return new DirectoryMeasure(0, 0, DateTime.MinValue, false); }
        var containsReparsePoint = false;
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(entry); }
                    catch { continue; }

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        containsReparsePoint = true;
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        latestWrite = Max(latestWrite, Directory.GetLastWriteTimeUtc(entry));
                        pending.Push(entry);
                        continue;
                    }

                    var info = new FileInfo(entry);
                    size += info.Length;
                    files++;
                    latestWrite = Max(latestWrite, info.LastWriteTimeUtc);
                }
            }
            catch
            {
                // Nicht lesbare Einzelordner werden bei der Groesse ausgelassen.
            }
        }

        return new DirectoryMeasure(size, files, latestWrite, containsReparsePoint);
    }

    private static ProjectMarkerScanResult ScanForProjectMarker(string path)
    {
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(current))
                {
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(entry); }
                    catch { return ProjectMarkerScanResult.Incomplete; }

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        return ProjectMarkerScanResult.Incomplete;

                    var name = Path.GetFileName(entry);
                    if ((attributes & FileAttributes.Directory) == 0)
                    {
                        if (name.Equals("projekt.json", StringComparison.OrdinalIgnoreCase)
                            || name.Equals("projekt.pointer", StringComparison.OrdinalIgnoreCase))
                        {
                            return ProjectMarkerScanResult.Found;
                        }
                        continue;

                    }

                    if (name.Equals("Projektdateien", StringComparison.OrdinalIgnoreCase))
                        return ProjectMarkerScanResult.Found;
                    pending.Push(entry);
                }
            }
            catch
            {
                return ProjectMarkerScanResult.Incomplete;
            }
        }

        return ProjectMarkerScanResult.NotFound;
    }

    private static void ClearReadOnlyAttributes(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", RecursiveFiles))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); }
            catch { /* Der anschliessende Delete liefert den konkreten Fehler. */ }
        }

        try
        {
            var attributes = File.GetAttributes(path);
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
        catch
        {
            // Der anschliessende Delete liefert den konkreten Fehler.
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string path, string pattern = "*")
    {
        try { return Directory.EnumerateDirectories(path, pattern, SearchOption.TopDirectoryOnly).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string path, string pattern, SearchOption option)
    {
        try { return Directory.EnumerateFiles(path, pattern, option).ToArray(); }
        catch { return Array.Empty<string>(); }
    }

    private static bool IsWorkspaceTempName(string name)
        => name.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
           || name.Equals("tmp", StringComparison.OrdinalIgnoreCase)
           || name.Equals(".codex-tmp", StringComparison.OrdinalIgnoreCase)
           || name.StartsWith(".tmp-", StringComparison.OrdinalIgnoreCase);

    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return true; }
    }

    private static bool IsDescendant(string path, string root)
        => !PathsEqual(path, root)
           && path.StartsWith(WithTrailingSeparator(root), StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrAncestor(string ancestor, string path)
        => PathsEqual(ancestor, path)
           || path.StartsWith(WithTrailingSeparator(ancestor), StringComparison.OrdinalIgnoreCase);

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static string WithTrailingSeparator(string path)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;

    private static long TryGetFileSize(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
        catch { return 0; }
    }

    private static DateTime Max(DateTime left, DateTime right) => left >= right ? left : right;

    private sealed record DirectoryMeasure(
        long SizeBytes,
        int FileCount,
        DateTime LatestWriteUtc,
        bool ContainsReparsePoint);

    private enum ProjectMarkerScanResult
    {
        NotFound,
        Found,
        Incomplete
    }

    private sealed record CleanupContext(
        string ProgramRoot,
        string SystemTempRoot,
        string CurrentAppBaseDirectory,
        IReadOnlyList<string> ProtectedProjectRoots,
        DateTime TempCutoffUtc)
    {
        public static CleanupContext Create(ProgramCleanupRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.ProgramRoot))
                throw new ArgumentException("Programmordner fehlt.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.SystemTempRoot))
                throw new ArgumentException("Windows-Temp-Ordner fehlt.", nameof(request));

            var programRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.ProgramRoot));
            if (!Directory.Exists(programRoot))
                throw new DirectoryNotFoundException($"Programmordner nicht gefunden: {programRoot}");

            var systemTempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.SystemTempRoot));
            var appBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.CurrentAppBaseDirectory));
            var protectedRoots = (request.ProtectedProjectRoots ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(TryNormalizePath)
                .Where(path => path is not null)
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new CleanupContext(
                programRoot,
                systemTempRoot,
                appBase,
                protectedRoots,
                request.TemporaryFileCutoffUtc ?? DateTime.UtcNow.AddDays(-1));
        }

        private static string? TryNormalizePath(string path)
        {
            try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
            catch { return null; }
        }

        public bool IsProtectedByProject(string path)
            => ProtectedProjectRoots.Any(root =>
                IsSameOrAncestor(root, path) || IsSameOrAncestor(path, root));
    }
}
