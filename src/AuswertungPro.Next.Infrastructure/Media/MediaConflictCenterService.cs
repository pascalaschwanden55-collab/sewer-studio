using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Media;

public sealed class MediaConflictCenterService
{
    public const string MappingMetadataKey = "VideoConflictMappingsV1";

    private static readonly Regex InfoFileNameRegex = new(
        @"^(?<date>\d{8})_(?<holding>.+?)_VIDEO_(?<kind>MISSING|AMBIGUOUS)\.txt$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HoldingPairRegex = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex NodePrefixRegex = new(
        @"^\d{1,2}\.",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public enum ConflictType
    {
        Missing,
        Ambiguous
    }

    public sealed record MediaConflictCase(
        string InfoPath,
        string HoldingFolder,
        string HoldingFolderName,
        string? HoldingRaw,
        string? SourcePdfPath,
        string? DateStamp,
        DateTime? Date,
        string? ExpectedVideoName,
        ConflictType Type,
        IReadOnlyList<string> Candidates,
        string Fingerprint);

    public sealed record ResolveResult(
        bool Success,
        string Message,
        string? SourceVideoPath,
        string? DestVideoPath,
        string? UpdatedHolding,
        string? InfoPath);

    public sealed record AutoResolveResult(
        int TotalConflicts,
        int Resolved,
        int Failed,
        int Unresolved,
        IReadOnlyList<string> Messages);

    public sealed record LearnedVideoMapping(
        string Fingerprint,
        string SelectedFileName,
        string? LastKnownSourcePath,
        DateTime LearnedAtUtc);

    private sealed class MappingStore
    {
        public Dictionary<string, LearnedVideoMapping> ByFingerprint { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, LearnedVideoMapping> ByFilmName { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<MediaConflictCase> Scan(string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFolder))
            return Array.Empty<MediaConflictCase>();

        string holdingsRoot;
        try
        {
            var guard = new ProjectWritePathGuard(projectFolder);
            holdingsRoot = guard.EnsureSafeDirectoryTarget(
                Path.Combine(projectFolder, "Haltungen"));
        }
        catch
        {
            return Array.Empty<MediaConflictCase>();
        }

        if (!Directory.Exists(holdingsRoot))
            return Array.Empty<MediaConflictCase>();

        var infoFiles = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(holdingsRoot, "*_VIDEO_*.txt", recursive: true)
            .Where(path =>
                path.EndsWith("_VIDEO_MISSING.txt", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("_VIDEO_AMBIGUOUS.txt", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var list = new List<MediaConflictCase>();
        foreach (var infoPath in infoFiles)
        {
            try
            {
                var parsed = ParseConflictInfo(infoPath);
                if (parsed is not null)
                    list.Add(parsed);
            }
            catch
            {
                // Skip malformed conflict files.
            }
        }

        return list
            .OrderByDescending(x => x.DateStamp ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.HoldingFolderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InfoPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public int GetMappingCount(Project project)
    {
        var store = LoadMappings(project);
        return store.ByFingerprint.Count;
    }

    public int ClearMappings(Project project)
    {
        if (project is null)
            return 0;

        var old = LoadMappings(project);
        var count = old.ByFingerprint.Count;
        project.Metadata.Remove(MappingMetadataKey);
        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        return count;
    }

    public string? TryResolveLearnedSourcePath(
        Project project,
        MediaConflictCase conflict,
        string? preferredVideoRoot = null,
        IReadOnlyDictionary<string, List<string>>? fileIndexByName = null)
    {
        if (project is null || conflict is null)
            return null;

        var store = LoadMappings(project);
        if (!TryGetLearnedMapping(store, conflict, out var learned))
            return null;
        if (!TryNormalizeLearnedVideoFileName(learned.SelectedFileName, out var selectedFileName))
            return null;

        // 1) Last known path.
        if (TryInspectNamedVideoSource(
                learned.LastKnownSourcePath,
                selectedFileName,
                out var lastKnownSourcePath))
        {
            return lastKnownSourcePath;
        }

        // 2) Current candidates from info/unmatched.
        foreach (var candidate in conflict.Candidates ?? Array.Empty<string>())
        {
            if (TryInspectNamedVideoSource(candidate, selectedFileName, out var candidateMatch))
                return candidateMatch;
        }

        // 3) Search indexed source roots.
        if (fileIndexByName is not null && fileIndexByName.TryGetValue(selectedFileName, out var hits))
        {
            foreach (var hit in hits ?? [])
            {
                if (TryInspectNamedVideoSource(hit, selectedFileName, out var indexedSourcePath))
                    return indexedSourcePath;
            }
        }

        // 4) Last fallback: targeted search in preferred root.
        if (TryInspectExistingSourceDirectory(
                preferredVideoRoot,
                out var safePreferredVideoRoot,
                out _))
        {
            foreach (var found in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                         .EnumerateFilesSafe(safePreferredVideoRoot, selectedFileName, recursive: true)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (TryInspectNamedVideoSource(found, selectedFileName, out var foundSourcePath))
                    return foundSourcePath;
            }
        }

        return null;
    }

    public AutoResolveResult AutoResolveLearned(
        Project project,
        string projectFolder,
        string? preferredVideoRoot = null,
        bool setUserEdited = false)
    {
        var conflicts = Scan(projectFolder);
        if (conflicts.Count == 0)
            return new AutoResolveResult(0, 0, 0, 0, Array.Empty<string>());

        IReadOnlyDictionary<string, List<string>>? index = null;
        if (TryInspectExistingSourceDirectory(preferredVideoRoot, out var safePreferredVideoRoot, out _))
            index = BuildVideoFileIndex(safePreferredVideoRoot);

        var messages = new List<string>();
        var resolved = 0;
        var failed = 0;

        foreach (var conflict in conflicts)
        {
            var source = TryResolveLearnedSourcePath(project, conflict, preferredVideoRoot, index);
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var result = ResolveConflict(
                project,
                projectFolder,
                conflict,
                source,
                setUserEdited);
            if (result.Success)
            {
                resolved++;
                messages.Add($"OK: {conflict.HoldingFolderName} -> {Path.GetFileName(result.DestVideoPath ?? source)}");
            }
            else
            {
                failed++;
                messages.Add($"FAIL: {conflict.HoldingFolderName} - {result.Message}");
            }
        }

        var unresolved = Math.Max(0, conflicts.Count - resolved - failed);
        return new AutoResolveResult(conflicts.Count, resolved, failed, unresolved, messages);
    }

    public ResolveResult ResolveConflict(
        Project project,
        MediaConflictCase conflict,
        string selectedVideoPath,
        bool setUserEdited = true)
    {
        if (conflict is null)
        {
            return new ResolveResult(
                false,
                "Konflikt ist null.",
                selectedVideoPath,
                null,
                null,
                null);
        }

        var projectRoot = FindProjectRoot(conflict.HoldingFolder);
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return new ResolveResult(
                false,
                "Der Projektordner des Medienkonflikts konnte nicht sicher bestimmt werden.",
                selectedVideoPath,
                null,
                null,
                conflict?.InfoPath);
        }

        return ResolveConflict(
            project,
            projectRoot,
            conflict,
            selectedVideoPath,
            setUserEdited);
    }

    public ResolveResult ResolveConflict(
        Project project,
        string? projectFolder,
        MediaConflictCase conflict,
        string selectedVideoPath,
        bool setUserEdited = true)
    {
        if (project is null)
            return new ResolveResult(false, "Projekt ist null.", selectedVideoPath, null, null, conflict?.InfoPath);
        if (conflict is null)
            return new ResolveResult(false, "Konflikt ist null.", selectedVideoPath, null, null, null);
        if (string.IsNullOrWhiteSpace(projectFolder))
            return new ResolveResult(false, "Projektordner fehlt.", selectedVideoPath, null, null, conflict.InfoPath);
        if (!TryInspectExistingVideoSource(selectedVideoPath, out var safeSelectedVideoPath, out var sourceError))
        {
            return new ResolveResult(
                false,
                sourceError ?? "Videoquelle konnte nicht sicher geprueft werden.",
                selectedVideoPath,
                null,
                null,
                conflict.InfoPath);
        }

        selectedVideoPath = safeSelectedVideoPath;

        try
        {
            var guard = new ProjectWritePathGuard(projectFolder);
            var holdingFolder = guard.EnsureSafeDirectoryTarget(conflict.HoldingFolder);
            var infoPath = guard.EnsureSafeFileTarget(conflict.InfoPath);
            if (!Directory.Exists(holdingFolder))
            {
                return new ResolveResult(
                    false,
                    $"Haltungsordner nicht gefunden: {conflict.HoldingFolder}",
                    selectedVideoPath,
                    null,
                    null,
                    conflict.InfoPath);
            }

            var targetRecord = FindRecord(project, conflict);
            if (targetRecord is null)
            {
                return new ResolveResult(
                    false,
                    "Kein passender Haltungsdatensatz gefunden; der Konflikt bleibt offen.",
                    selectedVideoPath,
                    null,
                    null,
                    conflict.InfoPath);
            }

            var ext = Path.GetExtension(selectedVideoPath);

            var stem = BuildDestStem(conflict);
            var existing = FindExistingVideo(holdingFolder, selectedVideoPath, guard);
            string destVideoPath;
            if (!string.IsNullOrWhiteSpace(existing))
            {
                destVideoPath = guard.EnsureSafeFileTarget(existing);
            }
            else
            {
                var desired = guard.EnsureSafeFileTarget(
                    Path.Combine(holdingFolder, $"{stem}{ext}"));
                destVideoPath = guard.EnsureSafeFileTarget(
                    EnsureUniquePath(desired, overwrite: false));
                if (!string.Equals(
                        Path.GetFullPath(selectedVideoPath),
                        Path.GetFullPath(destVideoPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    guard.EnsureSafeFileTarget(destVideoPath);
                    if (!TryInspectExistingVideoSource(selectedVideoPath, out var recheckedSourcePath, out sourceError))
                        throw new IOException(sourceError ?? "Videoquelle konnte vor dem Kopieren nicht sicher geprueft werden.");
                    File.Copy(recheckedSourcePath, destVideoPath, overwrite: false);
                }
            }

            var updatedHolding = UpdateRecordLink(
                targetRecord,
                destVideoPath,
                projectFolder,
                setUserEdited);

            try
            {
                guard.EnsureSafeFileTarget(infoPath);
                if (File.Exists(infoPath))
                    File.Delete(infoPath);
            }
            catch
            {
                // Non-fatal.
            }

            Learn(project, conflict, selectedVideoPath);

            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;

            return new ResolveResult(
                true,
                "Konflikt aufgeloest.",
                selectedVideoPath,
                destVideoPath,
                updatedHolding,
                conflict.InfoPath);
        }
        catch (Exception ex)
        {
            return new ResolveResult(false, ex.Message, selectedVideoPath, null, null, conflict.InfoPath);
        }
    }

    private static MediaConflictCase? ParseConflictInfo(string infoPath)
    {
        if (string.IsNullOrWhiteSpace(infoPath) || !File.Exists(infoPath))
            return null;

        var fileName = Path.GetFileName(infoPath);
        var fileMatch = InfoFileNameRegex.Match(fileName);

        var holdingFolder = Path.GetDirectoryName(infoPath);
        if (string.IsNullOrWhiteSpace(holdingFolder))
            return null;

        var holdingFolderName = Path.GetFileName(holdingFolder);
        var dateStamp = fileMatch.Success ? fileMatch.Groups["date"].Value : null;
        var type = fileMatch.Success && string.Equals(fileMatch.Groups["kind"].Value, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase)
            ? ConflictType.Ambiguous
            : ConflictType.Missing;

        string? sourcePdf = null;
        string? film = null;
        string? holdingRaw = null;
        DateTime? date = ParseDateStamp(dateStamp);

        var candidates = new List<string>();
        var inCandidates = false;

        foreach (var raw in File.ReadLines(infoPath, Encoding.UTF8))
        {
            var line = (raw ?? string.Empty).Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("PDF:", StringComparison.OrdinalIgnoreCase))
            {
                sourcePdf = line["PDF:".Length..].Trim();
                inCandidates = false;
                continue;
            }

            if (line.StartsWith("Film:", StringComparison.OrdinalIgnoreCase))
            {
                film = line["Film:".Length..].Trim();
                inCandidates = false;
                continue;
            }

            if (line.StartsWith("Haltung:", StringComparison.OrdinalIgnoreCase))
            {
                holdingRaw = line["Haltung:".Length..].Trim();
                inCandidates = false;
                continue;
            }

            if (line.StartsWith("Datum:", StringComparison.OrdinalIgnoreCase))
            {
                var rawDate = line["Datum:".Length..].Trim();
                if (DateTime.TryParseExact(rawDate, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    date = parsed;
                inCandidates = false;
                continue;
            }

            if (line.StartsWith("Candidates:", StringComparison.OrdinalIgnoreCase))
            {
                inCandidates = true;
                continue;
            }

            if (inCandidates && line.StartsWith("-", StringComparison.Ordinal))
            {
                var candidatePath = line[1..].Trim();
                if (!string.IsNullOrWhiteSpace(candidatePath))
                    candidates.Add(candidatePath);
            }
        }

        var mergedCandidates = new List<string>();
        foreach (var c in candidates)
        {
            if (!mergedCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                mergedCandidates.Add(c);
        }

        var siblingUnmatched = FindUnmatchedCandidates(
            holdingFolder,
            holdingFolderName,
            dateStamp);
        foreach (var c in siblingUnmatched)
        {
            if (!mergedCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                mergedCandidates.Add(c);
        }

        var normalizedFilm = NormalizeVideoFileName(film);
        if (string.Equals(normalizedFilm, "<nicht gefunden>", StringComparison.OrdinalIgnoreCase))
            normalizedFilm = null;

        var effectiveHolding = string.IsNullOrWhiteSpace(holdingRaw) ? holdingFolderName : holdingRaw;
        var fingerprint = BuildFingerprint(dateStamp, effectiveHolding, normalizedFilm);

        return new MediaConflictCase(
            InfoPath: infoPath,
            HoldingFolder: holdingFolder,
            HoldingFolderName: holdingFolderName,
            HoldingRaw: holdingRaw,
            SourcePdfPath: sourcePdf,
            DateStamp: dateStamp,
            Date: date,
            ExpectedVideoName: normalizedFilm,
            Type: type,
            Candidates: mergedCandidates,
            Fingerprint: fingerprint);
    }

    private static IReadOnlyList<string> FindUnmatchedCandidates(
        string holdingFolder,
        string holdingFolderName,
        string? dateStamp)
    {
        try
        {
            var gemeindeFolder = Directory.GetParent(holdingFolder)?.FullName;
            if (string.IsNullOrWhiteSpace(gemeindeFolder))
                return Array.Empty<string>();

            var unmatchedFolder = Path.Combine(gemeindeFolder, "__UNMATCHED", holdingFolderName);
            if (!Directory.Exists(unmatchedFolder))
                return Array.Empty<string>();

            var prefix = !string.IsNullOrWhiteSpace(dateStamp)
                ? $"{dateStamp}_{holdingFolderName}_CANDIDATE_"
                : $"{holdingFolderName}_CANDIDATE_";

            var files = Directory.EnumerateFiles(unmatchedFolder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count > 0)
                return files;

            return Directory.EnumerateFiles(unmatchedFolder, "*CANDIDATE*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildDestStem(MediaConflictCase conflict)
    {
        var datePart = conflict.DateStamp;
        if (string.IsNullOrWhiteSpace(datePart) && conflict.Date.HasValue)
            datePart = conflict.Date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(datePart))
            datePart = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return $"{datePart}_{conflict.HoldingFolderName}";
    }

    private static string? UpdateRecordLink(
        HaltungRecord record,
        string destVideoPath,
        string projectRoot,
        bool setUserEdited)
    {
        var storedPath = ProjectPathResolver.MakeRelativeIfInsideProject(destVideoPath, projectRoot);
        record.SetFieldValue("Link", storedPath, FieldSource.Unknown, userEdited: setUserEdited);
        return record.GetFieldValue("Haltungsname");
    }

    private static string? FindProjectRoot(string holdingFolder)
    {
        try
        {
            var current = new DirectoryInfo(Path.GetFullPath(holdingFolder));
            while (current is not null)
            {
                if (current.Name is "Haltungen" or "Haltungen_Verteilt")
                    return current.Parent?.FullName;
                current = current.Parent;
            }
        }
        catch
        {
            // Unbekannte Altstruktur: absoluten Pfad beibehalten.
        }

        return null;
    }

    private static HaltungRecord? FindRecord(Project project, MediaConflictCase conflict)
    {
        var candidateKeys = new List<string>();
        if (!string.IsNullOrWhiteSpace(conflict.HoldingRaw))
            candidateKeys.Add(conflict.HoldingRaw);
        candidateKeys.Add(conflict.HoldingFolderName);

        var normalized = candidateKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => SanitizePathSegment(NormalizeHaltungId(x)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var stripped = normalized
            .Select(StripNodePrefixes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var rec in project.Data)
        {
            var recHolding = rec.GetFieldValue("Haltungsname");
            var recKey = SanitizePathSegment(NormalizeHaltungId(recHolding));
            var recStripped = StripNodePrefixes(recKey);

            if (normalized.Contains(recKey, StringComparer.OrdinalIgnoreCase))
                return rec;
            if (stripped.Contains(recStripped, StringComparer.OrdinalIgnoreCase))
                return rec;
        }

        return null;
    }

    private void Learn(Project project, MediaConflictCase conflict, string selectedVideoPath)
    {
        var fileName = Path.GetFileName(selectedVideoPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var store = LoadMappings(project);
        var mapping = new LearnedVideoMapping(
            Fingerprint: conflict.Fingerprint,
            SelectedFileName: fileName,
            LastKnownSourcePath: selectedVideoPath,
            LearnedAtUtc: DateTime.UtcNow);

        store.ByFingerprint[conflict.Fingerprint] = mapping;
        var filmKey = BuildFilmLookupKey(conflict.ExpectedVideoName);
        if (!string.IsNullOrWhiteSpace(filmKey))
            store.ByFilmName[filmKey] = mapping;

        SaveMappings(project, store);
    }

    private static bool TryGetLearnedMapping(
        MappingStore store,
        MediaConflictCase conflict,
        out LearnedVideoMapping mapping)
    {
        if (store.ByFingerprint.TryGetValue(conflict.Fingerprint, out mapping!))
            return true;

        var filmKey = BuildFilmLookupKey(conflict.ExpectedVideoName);
        if (!string.IsNullOrWhiteSpace(filmKey)
            && store.ByFilmName.TryGetValue(filmKey, out mapping!))
        {
            return true;
        }

        mapping = null!;
        return false;
    }

    private static MappingStore LoadMappings(Project project)
    {
        if (project is null || !project.Metadata.TryGetValue(MappingMetadataKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return new MappingStore();

        try
        {
            var store = JsonSerializer.Deserialize<MappingStore>(raw, JsonOptions) ?? new MappingStore();
            store.ByFingerprint = store.ByFingerprint is null
                ? new Dictionary<string, LearnedVideoMapping>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, LearnedVideoMapping>(store.ByFingerprint, StringComparer.OrdinalIgnoreCase);
            store.ByFilmName = store.ByFilmName is null
                ? new Dictionary<string, LearnedVideoMapping>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, LearnedVideoMapping>(store.ByFilmName, StringComparer.OrdinalIgnoreCase);
            return store;
        }
        catch
        {
            return new MappingStore();
        }
    }

    private static void SaveMappings(Project project, MappingStore store)
    {
        if (project is null)
            return;

        var json = JsonSerializer.Serialize(store, JsonOptions);
        project.Metadata[MappingMetadataKey] = json;
    }

    private static string BuildFingerprint(string? dateStamp, string? holding, string? expectedVideoName)
    {
        var d = (dateStamp ?? string.Empty).Trim();
        var h = SanitizePathSegment(NormalizeHaltungId(holding));
        var v = BuildFilmLookupKey(expectedVideoName) ?? string.Empty;
        return $"{d}|{h}|{v}";
    }

    private static string? BuildFilmLookupKey(string? value)
    {
        var normalized = NormalizeVideoFileName(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized.ToLowerInvariant();
    }

    // Delegiert an HoldingTextNormalizer – identische Logik, zentralisiert.
    private static string? NormalizeVideoFileName(string? value)
        => HoldingTextNormalizer.NormalizeVideoFileName(value);

    private static DateTime? ParseDateStamp(string? dateStamp)
    {
        if (string.IsNullOrWhiteSpace(dateStamp))
            return null;

        return DateTime.TryParseExact(
            dateStamp,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var text = NormalizeText(value).Trim();
        var m = HoldingPairRegex.Match(text);
        if (m.Success)
        {
            var normalized = m.Groups[1].Value.Replace(" ", "").Replace("/", "-");
            normalized = Regex.Replace(normalized, @"\s*-+\s*", "-", RegexOptions.CultureInvariant);
            return normalized;
        }

        return text;
    }

    private static string NormalizeText(string value)
        => value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

    private static string SanitizePathSegment(string? value)
        => ProjectPathResolver.SanitizePathSegment(value);

    private static string StripNodePrefixes(string holdingKey)
    {
        if (string.IsNullOrWhiteSpace(holdingKey))
            return string.Empty;

        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRegex.Replace(holdingKey, "");

        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        left = NodePrefixRegex.Replace(left, "");
        right = NodePrefixRegex.Replace(right, "");
        return $"{left}-{right}";
    }

    private static readonly HashSet<string> VideoExtensions = new(MediaFileTypes.VideoExtensions, StringComparer.OrdinalIgnoreCase);

    private static bool TryInspectNamedVideoSource(
        string? path,
        string expectedFileName,
        out string safePath)
    {
        safePath = string.Empty;
        if (!TryInspectExistingVideoSource(path, out var inspectedPath, out _))
            return false;
        if (!string.Equals(
                Path.GetFileName(inspectedPath),
                expectedFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        safePath = inspectedPath;
        return true;
    }

    private static bool TryInspectExistingVideoSource(
        string? path,
        out string safePath,
        out string? error)
    {
        safePath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Video nicht gefunden: Der Quellenpfad fehlt.";
            return false;
        }

        if (!HasAllowedVideoExtension(path))
        {
            error = "Nicht erlaubte Video-Endung.";
            return false;
        }

        if (!ImportSourcePathGuard.TryInspectFile(
                path,
                out var inspectedPath,
                out var exists,
                out error))
        {
            return false;
        }

        if (!exists)
        {
            error = $"Video nicht gefunden: {path}";
            return false;
        }

        safePath = inspectedPath;
        return true;
    }

    private static bool TryInspectExistingSourceDirectory(
        string? path,
        out string safePath,
        out string? error)
    {
        safePath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Video-Quellordner fehlt.";
            return false;
        }

        if (!ImportSourcePathGuard.TryInspectDirectory(
                path,
                out var inspectedPath,
                out var exists,
                out error))
        {
            return false;
        }

        if (!exists)
        {
            error = $"Video-Quellordner nicht gefunden: {path}";
            return false;
        }

        safePath = inspectedPath;
        return true;
    }

    private static bool TryNormalizeLearnedVideoFileName(string? value, out string fileName)
    {
        fileName = (value ?? string.Empty).Trim();
        if (fileName.Length == 0
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || !HasAllowedVideoExtension(fileName))
        {
            fileName = string.Empty;
            return false;
        }

        return true;
    }

    private static bool HasAllowedVideoExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            return VideoExtensions.Contains(Path.GetExtension(path.Trim()));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string? FindExistingVideo(
        string holdingFolder,
        string sourceVideoPath,
        ProjectWritePathGuard guard)
    {
        if (!Directory.Exists(holdingFolder))
            return null;
        if (!TryInspectExistingVideoSource(sourceVideoPath, out var safeSourceVideoPath, out _))
            return null;

        try
        {
            var safeHoldingFolder = guard.EnsureSafeDirectoryTarget(holdingFolder);
            foreach (var candidate in Directory.EnumerateFiles(safeHoldingFolder))
            {
                var existing = guard.EnsureSafeFileTarget(candidate);
                var ext = Path.GetExtension(existing);
                if (!VideoExtensions.Contains(ext))
                    continue;

                // Nur vollständig gleicher Inhalt darf als bereits vorhandenes Kundenmedium gelten.
                if (FileContentComparer.FilesEqual(safeSourceVideoPath, existing))
                    return existing;
            }
        }
        catch
        {
            // Non-fatal.
        }

        return null;
    }

    private static string EnsureUniquePath(string path, bool overwrite)
    {
        if (overwrite || !File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i:00}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Unable to find free filename for {path}");
    }

    private static IReadOnlyDictionary<string, List<string>> BuildVideoFileIndex(string root)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryInspectExistingSourceDirectory(root, out var safeRoot, out _))
            return map;

        foreach (var file in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(safeRoot, "*.*", recursive: true))
        {
            if (!TryInspectExistingVideoSource(file, out var safeFile, out _))
                continue;

            var name = Path.GetFileName(safeFile);
            if (!map.TryGetValue(name, out var list))
            {
                list = new List<string>();
                map[name] = list;
            }

            list.Add(safeFile);
        }

        return map;
    }
}
