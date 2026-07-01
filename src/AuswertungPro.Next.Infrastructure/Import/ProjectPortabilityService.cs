using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Macht ein Projekt portabel ("1:1 auf anderen PC übertragbar"): setzt ALLE Medienpfade
/// (Link/Video, PDF, Fotos) auf RELATIVE Pfade, die auf die im Projekt liegende Kopie zeigen.
/// - Video/PDF: auf die bereits verteilte Kopie im Haltungsordner umbiegen (KEIN Neu-Kopieren).
/// - Fotos, die nur in der Quelle (absolut, ausserhalb) liegen: in den Haltungsordner kopieren.
/// - Absolute Pfade INNERHALB des Projekts: einfach relativ machen.
/// Danach loest beim Öffnen alles relativ zum Projektordner auf, egal auf welchem PC/Laufwerk.
/// </summary>
public sealed class ProjectPortabilityService
{
    public sealed record Result(int RelinkedPaths, int FotosCopied, int Unresolved, IReadOnlyList<string> Messages);

    private enum Act { Kept, Relinked, Copied, Unresolved }

    // Reihenfolge der bekannten Haltungs-Wurzelordner im Projekt.
    private static readonly string[] HoldingRootFolders = { "Verteilung", "Haltungen" };

    public Result MakePortable(string projectFolder, Project project, bool dryRun = false)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(projectFolder) || project is null)
            return new Result(0, 0, 0, new[] { "Kein Projektordner/Projekt." });

        var relinked = 0;
        var fotosCopied = 0;
        var unresolved = 0;

        void Tally(Act act)
        {
            switch (act)
            {
                case Act.Relinked: relinked++; break;
                case Act.Copied: fotosCopied++; break;
                case Act.Unresolved: unresolved++; break;
            }
        }

        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            var holdingFolder = ResolveHoldingFolder(projectFolder, san);

            // Video + PDF: auf die Projekt-Kopie im Haltungsordner umbiegen (nicht neu kopieren).
            RelinkField(record, FieldKeys.Link, holdingFolder, projectFolder, IsVideo, copyExternalFotos: false, dryRun, Tally, messages);
            RelinkField(record, FieldKeys.PdfPath, holdingFolder, projectFolder, IsPdf, copyExternalFotos: false, dryRun, Tally, messages);
            RelinkFieldList(record, FieldKeys.PdfAll, holdingFolder, projectFolder, IsPdf, dryRun, Tally, messages);

            // Fotos: Pro-Befund-Bindung bleibt, nur Pfad relativ (Quell-Foto ggf. ins Projekt kopieren).
            if (record.Protocol != null)
            {
                RelinkRevisionFotos(record.Protocol.Original, holdingFolder, projectFolder, dryRun, Tally, messages);
                RelinkRevisionFotos(record.Protocol.Current, holdingFolder, projectFolder, dryRun, Tally, messages);
                foreach (var rev in record.Protocol.History)
                    RelinkRevisionFotos(rev, holdingFolder, projectFolder, dryRun, Tally, messages);
            }

            if (record.VsaFindings != null)
            {
                foreach (var finding in record.VsaFindings)
                {
                    if (string.IsNullOrWhiteSpace(finding.FotoPath))
                        continue;
                    var (val, act) = ResolvePortable(finding.FotoPath, holdingFolder, projectFolder, IsImage, copyExternalInto: "Fotos", dryRun);
                    if (!dryRun && act is Act.Relinked or Act.Copied)
                        finding.FotoPath = val;
                    Tally(act);
                }
            }
        }

        if (!dryRun)
            project.Dirty = true;

        return new Result(relinked, fotosCopied, unresolved, messages);
    }

    private static string? ResolveHoldingFolder(string projectFolder, string san)
    {
        foreach (var root in HoldingRootFolders)
        {
            var p = Path.Combine(projectFolder, root, san);
            if (Directory.Exists(p))
                return p;
        }
        return null;
    }

    private void RelinkField(
        HaltungRecord record, string field, string? holdingFolder, string projectFolder,
        Func<string, bool> typeMatch, bool copyExternalFotos, bool dryRun,
        Action<Act> tally, List<string> messages)
    {
        var raw = record.GetFieldValue(field)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var (val, act) = ResolvePortable(raw, holdingFolder, projectFolder, typeMatch,
            copyExternalInto: copyExternalFotos ? "Fotos" : null, dryRun);
        if (!dryRun && act is Act.Relinked or Act.Copied)
            record.SetFieldValue(field, val, FieldSource.Legacy, userEdited: false);
        if (act == Act.Unresolved)
            messages.Add($"{field}: nicht aufgeloest ({raw})");
        tally(act);
    }

    private void RelinkFieldList(
        HaltungRecord record, string field, string? holdingFolder, string projectFolder,
        Func<string, bool> typeMatch, bool dryRun, Action<Act> tally, List<string> messages)
    {
        var raw = record.GetFieldValue(field)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var newParts = new List<string>(parts.Length);
        var changed = false;

        foreach (var p in parts)
        {
            var (val, act) = ResolvePortable(p.Trim(), holdingFolder, projectFolder, typeMatch, copyExternalInto: null, dryRun);
            newParts.Add(val);
            if (act is Act.Relinked or Act.Copied) changed = true;
            if (act == Act.Unresolved) messages.Add($"{field}: nicht aufgeloest ({p.Trim()})");
            tally(act);
        }

        if (changed && !dryRun)
            record.SetFieldValue(field, string.Join(";", newParts), FieldSource.Legacy, userEdited: false);
    }

    private void RelinkRevisionFotos(
        ProtocolRevision revision, string? holdingFolder, string projectFolder,
        bool dryRun, Action<Act> tally, List<string> messages)
    {
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var raw = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var (val, act) = ResolvePortable(raw, holdingFolder, projectFolder, IsImage, copyExternalInto: "Fotos", dryRun);
                if (!dryRun && act is Act.Relinked or Act.Copied)
                    entry.FotoPaths[i] = val;
                if (act == Act.Unresolved) messages.Add($"Foto: nicht aufgeloest ({raw})");
                tally(act);
            }
        }
    }

    /// <summary>
    /// Liefert den portablen (relativen) Pfad + die durchgefuehrte Aktion.
    /// </summary>
    private (string Value, Act Act) ResolvePortable(
        string raw, string? holdingFolder, string projectFolder,
        Func<string, bool> typeMatch, string? copyExternalInto, bool dryRun)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
            return (raw, Act.Kept);

        // 1) Schon relativ + loest auf -> behalten.
        if (ProjectPathResolver.IsRelative(raw))
        {
            if (ProjectPathResolver.ResolveFilePathFromProjectFolder(raw, projectFolder) != null)
                return (raw, Act.Kept);
        }
        else
        {
            // 2) Absolut INNERHALB des Projekts -> nur relativ machen.
            string full;
            try { full = Path.GetFullPath(raw); } catch { full = raw; }
            var rootFull = Path.GetFullPath(projectFolder);
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                return (ProjectPathResolver.MakeRelative(full, projectFolder), Act.Relinked);
        }

        var fileName = Path.GetFileName(raw);

        // 3) Kopie im Haltungsordner finden (gleicher Typ) -> relinken (kein Neu-Kopieren).
        if (!string.IsNullOrWhiteSpace(holdingFolder) && Directory.Exists(holdingFolder))
        {
            var match = PickHoldingMatch(holdingFolder!, fileName, typeMatch);
            if (match != null)
                return (ProjectPathResolver.MakeRelative(match, projectFolder), Act.Relinked);
        }

        // 4) Foto-Sonderfall: absolut + extern existiert -> in den Haltungsordner kopieren.
        if (copyExternalInto != null && Path.IsPathRooted(raw) && File.Exists(raw)
            && !string.IsNullOrWhiteSpace(holdingFolder))
        {
            var destDir = Path.Combine(holdingFolder!, copyExternalInto);
            if (dryRun)
                return (raw, Act.Copied);
            try
            {
                Directory.CreateDirectory(destDir);
                var dest = CopyUnique(raw, destDir);
                return (ProjectPathResolver.MakeRelative(dest, projectFolder), Act.Copied);
            }
            catch
            {
                return (raw, Act.Unresolved);
            }
        }

        return (raw, Act.Unresolved);
    }

    private static string? PickHoldingMatch(string holdingFolder, string originalFileName, Func<string, bool> typeMatch)
    {
        string[] files;
        try { files = Directory.GetFiles(holdingFolder); }
        catch { return null; }

        var typed = files.Where(typeMatch).ToList();
        if (typed.Count == 0)
            return null;

        // Gegeninspektion (_G), Kandidaten und Ambiguous-Marker aussortieren -> Haupt-Kopie bevorzugen.
        var main = typed.Where(f =>
        {
            var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
            return !n.Contains("candidate") && !n.Contains("ambiguous") && !n.EndsWith("_g");
        }).ToList();
        var pool = main.Count > 0 ? main : typed;

        // Wenn der Originaldateiname (ohne Endung) exakt vorkommt, den bevorzugen.
        var stem = Path.GetFileNameWithoutExtension(originalFileName);
        var exact = pool.FirstOrDefault(f =>
            string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;

        return pool
            .OrderBy(f => Path.GetFileName(f).Length)
            .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static string CopyUnique(string source, string destDir)
    {
        var fileName = Path.GetFileName(source);
        var dest = Path.Combine(destDir, fileName);

        if (File.Exists(dest))
        {
            var srcInfo = new FileInfo(source);
            var destInfo = new FileInfo(dest);
            if (srcInfo.Length == destInfo.Length)
                return dest; // gleiche Datei -> wiederverwenden
        }

        if (!File.Exists(dest))
            File.Copy(source, dest, overwrite: false);
        return dest;
    }

    private static bool IsVideo(string path) => MediaFileTypes.HasVideoExtension(path);
    private static bool IsImage(string path) => MediaFileTypes.HasImageExtension(path);
    private static bool IsPdf(string path) => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
}
