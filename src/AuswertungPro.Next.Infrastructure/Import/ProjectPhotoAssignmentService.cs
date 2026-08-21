using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Ordnet Fotos aus einem Quellordner den Haltungen/Beobachtungen eines bestehenden Projekts zu —
/// per Dateiname (die Haltungsnummer steckt im Namen, z.B. H_&lt;Haltung&gt;_NNN.jpg). Format-agnostisch:
/// funktioniert für IKAS- wie WinCan-Exporte, solange das Foto die Haltung im Namen trägt.
/// Kopiert externe Fotos ins Projekt (&lt;Haltung&gt;/Fotos) und setzt RELATIVE FotoPaths an den
/// Beobachtungen → sofort sichtbar (Beobachtungen/AWU) und portabel. Fotos, die schon im Projekt
/// liegen, werden nur relativ verlinkt. GUID-benannte Fotos (nur über die DB zuordenbar) werden
/// hier NICHT erfasst — dafür ist der DB-Pfad (WinCan .db3 / IKAS FDB) zuständig.
/// </summary>
public sealed class ProjectPhotoAssignmentService : IProjectPhotoAssignmentService
{
    public sealed record Result(
        int HoldingsMatched,
        int PhotosAssigned,
        int PhotosCopied,
        int UnmatchedFiles,
        IReadOnlyList<string> Messages);

    private static readonly Regex IndexRx = new(@"_(\d+)\.[A-Za-z0-9]+$", RegexOptions.Compiled);

    public Result AssignFromFolder(string projectFolder, string sourceFolder, Project project)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(projectFolder) || string.IsNullOrWhiteSpace(sourceFolder) || project is null)
            return new Result(0, 0, 0, 0, new[] { "Projektordner/Quellordner/Projekt fehlt." });
        if (!Directory.Exists(sourceFolder))
            return new Result(0, 0, 0, 0, new[] { $"Quellordner nicht gefunden: {sourceFolder}" });

        var images = EnumerateImages(sourceFolder);
        if (images.Count == 0)
            return new Result(0, 0, 0, 0, new[] { "Keine Bilddateien im Quellordner gefunden." });

        var writePathGuard = new ProjectWritePathGuard(projectFolder);

        // Haltungs-Schlüssel je Record, längster zuerst (spezifischster Match gewinnt).
        var keyed = project.Data
            .Select(r => new { Record = r, Key = NormalizeKey(r.GetFieldValue("Haltungsname") ?? "") })
            .Where(x => x.Key.Length >= 4)
            .OrderByDescending(x => x.Key.Length)
            .ToList();

        var byRecord = new Dictionary<HaltungRecord, List<string>>();
        var unmatched = 0;
        foreach (var img in images)
        {
            var fileName = Path.GetFileNameWithoutExtension(img);
            var match = keyed.FirstOrDefault(x =>
                HoldingTextNormalizer.ContainsNormalizedKeyAtBoundary(fileName, x.Key));
            if (match is null) { unmatched++; continue; }
            if (!byRecord.TryGetValue(match.Record, out var list))
            {
                list = new List<string>();
                byRecord[match.Record] = list;
            }
            list.Add(img);
        }

        int holdings = 0, assigned = 0, copied = 0;
        foreach (var kv in byRecord)
        {
            var record = kv.Key;
            var san = ProjectPathResolver.SanitizePathSegment(record.GetFieldValue("Haltungsname") ?? "");
            // Fotos liegen GRUPPIERT je Haltung: <Projekt>\Fotos\Haltungen\<Haltung>\
            var fotoDir = ProjectStructure.FotosHaltungDir(projectFolder, san);

            var entries = GetEntries(record);
            if (entries.Count == 0)
            {
                messages.Add($"Haltung {san}: keine Beobachtungen vorhanden, {kv.Value.Count} Fotos uebersprungen.");
                continue;
            }

            var relPaths = new List<string>();
            foreach (var src in kv.Value.OrderBy(ExtractIndex))
            {
                try
                {
                    var rel = EnsureInProjectRelative(
                        src,
                        fotoDir,
                        projectFolder,
                        writePathGuard,
                        ref copied);
                    if (!string.IsNullOrWhiteSpace(rel) && !relPaths.Contains(rel, StringComparer.OrdinalIgnoreCase))
                        relPaths.Add(rel);
                }
                catch (Exception ex)
                {
                    messages.Add($"Foto-Kopierfehler {Path.GetFileName(src)}: {ex.Message}");
                }
            }
            if (relPaths.Count == 0)
                continue;

            assigned += DistributeToEntries(entries, relPaths);
            holdings++;
        }

        if (holdings > 0)
            project.Dirty = true;
        return new Result(holdings, assigned, copied, unmatched, messages);
    }

    ProjectPhotoAssignmentResult IProjectPhotoAssignmentService.AssignFromFolder(
        string projectFolder,
        string sourceFolder,
        Project project)
    {
        var result = AssignFromFolder(projectFolder, sourceFolder, project);
        return new ProjectPhotoAssignmentResult(
            result.HoldingsMatched,
            result.PhotosAssigned,
            result.PhotosCopied,
            result.UnmatchedFiles,
            result.Messages);
    }

    private static List<string> EnumerateImages(string root)
    {
        try
        {
            return AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                .EnumerateFilesSafe(root, "*.*", recursive: true)
                .Where(MediaFileTypes.HasImageExtension)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static int ExtractIndex(string path)
    {
        var m = IndexRx.Match(Path.GetFileName(path));
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    /// <summary>
    /// Liefert den RELATIVEN Projektpfad: liegt das Foto schon im Projekt → nur relativ machen;
    /// sonst nach &lt;Haltung&gt;/Fotos kopieren (kollisionssicher) und relativ verlinken.
    /// </summary>
    private static string? EnsureInProjectRelative(
        string src,
        string fotoDir,
        string projectFolder,
        ProjectWritePathGuard writePathGuard,
        ref int copied)
    {
        var srcFull = Path.GetFullPath(src);
        if (!File.Exists(srcFull))
            return null;

        var targetFull = writePathGuard.EnsureSafeDirectoryTarget(fotoDir);
        if (IsUnderDirectory(srcFull, targetFull))
            return ProjectPathResolver.MakeRelative(srcFull, projectFolder); // schon im gekoppelten Zielordner

        writePathGuard.EnsureSafeDirectoryTarget(targetFull);
        Directory.CreateDirectory(targetFull);
        var dest = writePathGuard.EnsureSafeFileTarget(
            Path.Combine(targetFull, Path.GetFileName(srcFull)));
        if (File.Exists(dest))
        {
            if (!SameFileContent(dest, srcFull))
            {
                var stem = Path.GetFileNameWithoutExtension(srcFull);
                var ext = Path.GetExtension(srcFull);
                dest = writePathGuard.EnsureSafeFileTarget(
                    Path.Combine(targetFull, $"{stem}_{Guid.NewGuid():N}".Substring(0, stem.Length + 7) + ext));
                writePathGuard.EnsureSafeFileTarget(dest);
                File.Copy(srcFull, dest, overwrite: false);
                copied++;
            }
            // Nur eine wirklich inhaltsgleiche Projektkopie wiederverwenden.
        }
        else
        {
            writePathGuard.EnsureSafeFileTarget(dest);
            File.Copy(srcFull, dest, overwrite: false);
            copied++;
        }
        return ProjectPathResolver.MakeRelative(dest, projectFolder);
    }

    private static bool SameFileContent(string left, string right)
    {
        try
        {
            return FileContentComparer.FilesEqual(left, right);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory);
        if (!fullDirectory.EndsWith(Path.DirectorySeparatorChar))
            fullDirectory += Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ProtocolEntry> GetEntries(HaltungRecord record)
    {
        var p = record.Protocol;
        if (p?.Current?.Entries is { Count: > 0 } c) return c;
        if (p?.Original?.Entries is { Count: > 0 } o) return o;
        return new List<ProtocolEntry>();
    }

    /// <summary>
    /// Verteilt Fotos auf die Beobachtungen: Einträge mit "foto"-Marker in der Beschreibung erhalten
    /// ihre Fotos (in Reihenfolge); übrige Fotos hängen an die erste Beobachtung (sichtbar + im AWU
    /// werden ohnehin alle Fotos der Haltung gesammelt).
    /// </summary>
    private static int DistributeToEntries(List<ProtocolEntry> entries, List<string> photos)
    {
        var queue = new Queue<string>(photos);
        var n = 0;

        foreach (var e in entries)
        {
            if (queue.Count == 0) break;
            if (!EntryWantsPhoto(e)) continue;
            var want = ExtractPhotoCount(e.Beschreibung);
            for (var i = 0; i < want && queue.Count > 0; i++)
            {
                if (AddOrReplacePhotoPath(e.FotoPaths, queue.Dequeue()))
                    n++;
            }
        }

        if (queue.Count > 0)
        {
            var first = entries[0];
            while (queue.Count > 0)
            {
                if (AddOrReplacePhotoPath(first.FotoPaths, queue.Dequeue()))
                    n++;
            }
        }

        return n;
    }

    private static bool AddOrReplacePhotoPath(IList<string> paths, string photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
            return false;

        var normalized = NormalizePath(photoPath);
        var fileName = Path.GetFileName(normalized.Replace('/', Path.DirectorySeparatorChar));
        for (var i = 0; i < paths.Count; i++)
        {
            var existing = NormalizePath(paths[i]);
            if (string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
                return false;

            var existingFile = Path.GetFileName(existing.Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrWhiteSpace(fileName)
                && string.Equals(existingFile, fileName, StringComparison.OrdinalIgnoreCase))
            {
                paths[i] = photoPath;
                RemoveDuplicatePhotoPaths(paths, normalized, keepIndex: i);
                return true;
            }
        }

        paths.Add(photoPath);
        return true;
    }

    private static void RemoveDuplicatePhotoPaths(IList<string> paths, string normalizedKeep, int keepIndex)
    {
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            if (i == keepIndex)
                continue;
            if (string.Equals(NormalizePath(paths[i]), normalizedKeep, StringComparison.OrdinalIgnoreCase))
                paths.RemoveAt(i);
        }
    }

    private static string NormalizePath(string path)
        => (path ?? string.Empty).Replace('\\', '/').Trim();

    private static bool EntryWantsPhoto(ProtocolEntry entry)
        => (entry.Beschreibung?.ToLowerInvariant() ?? "").Contains("foto");

    private static int ExtractPhotoCount(string? desc)
    {
        var m = Regex.Match(desc ?? "", @"foto\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var c) && c > 0 ? Math.Min(c, 5) : 1;
    }

    private static string NormalizeKey(string value) => HoldingTextNormalizer.NormalizeKey(value);
}
