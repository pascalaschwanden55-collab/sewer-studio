using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Eine Spiegel-Quelle: Quellordner → Ziel-Relativpfad, optional mit Ordner-Ausschluss.
/// Das Praedikat bekommt den Ordnerpfad RELATIV zum SourceRoot (z. B. "src\Foo\bin").
/// </summary>
public sealed record BackupSource(
    string SourceRoot,
    string TargetRelativeRoot,
    Func<string, bool>? IsDirExcluded = null,
    Func<string, bool>? IsFileExcluded = null,
    bool Required = true,
    bool WarnIfMissing = false);

/// <summary>Eine einzelne Datei: Quellpfad → Ziel-Relativpfad (z. B. Desktop-Skripte).</summary>
public sealed record BackupSingleFile(string SourcePath, string TargetRelativePath);

/// <summary>Eine Backup-Komponente (Programm, KI-Gehirn, Projekte, Einstellungen, Logs, Extras).</summary>
public sealed record BackupComponent(
    string Name,
    string Beschreibung,
    IReadOnlyList<BackupSource> Sources,
    IReadOnlyList<BackupSingleFile>? Files = null);

/// <summary>
/// Baut aus den aufgeloesten Quellpfaden den Sicherungsplan (welche Quelle wohin,
/// mit welchen Ausschluessen). Reine Logik, kein Dateisystemzugriff.
/// </summary>
public static class BackupPlanBuilder
{
    /// <summary>Name des Spiegel-Ordners im vom Nutzer gewaehlten Ziel.</summary>
    public const string TargetFolderName = "SewerStudio_Datensicherung";

    /// <summary>Marker-Datei im Spiegel-Root — ohne sie wird im Ziel NIE geloescht.</summary>
    public const string MarkerFileName = ".sewerstudio-datensicherung";

    /// <summary>Desktop-Startskripte, die (falls vorhanden) nach Extras\ gesichert werden.</summary>
    public static readonly IReadOnlyList<string> DesktopScriptNames =
        new[] { "SewerStudio.bat", "Start_SewerStudio.bat", "Backup_KI_BRAIN.bat" };

    public static IReadOnlyList<BackupComponent> Build(FullBackupSources sources)
    {
        var components = new List<BackupComponent>
        {
            new(
                "Programm",
                "Quellcode inkl. Git-Verlauf und Sidecar-Modelle (ohne Build-Artefakte)",
                sources.RepoRoot is null
                    ? Array.Empty<BackupSource>()
                    : new[] { new BackupSource(sources.RepoRoot, "Programm", BackupExclusionRules.IsProgramDirExcluded) }),

            new(
                "KI-Gehirn",
                "Wissensdatenbank, Gold-Labels, Eval-Set, trainierte Modelle (ohne regenerierbare Trainings-Datensaetze)",
                new[] { new BackupSource(sources.KnowledgeRoot, "KI_BRAIN", BackupExclusionRules.IsKiBrainDirExcluded) }),

            new(
                "Projekte",
                sources.IncludeProjectVideos
                    ? "Projektdateien, Fotos, Restore-Points und Videos (Videos enthalten: ja)"
                    : "Projektdateien, Fotos und Restore-Points (Videos enthalten: nein)",
                BuildProjectSources(
                    sources.ProjectRoots,
                    sources.OptionalProjectRoots,
                    sources.IncludeProjectVideos)),

            new(
                "Einstellungen",
                "App-Einstellungen, Presets, Dropdowns, Preiskataloge, Vorlagen, Kataster-Tabelle",
                new[]
                {
                    new BackupSource(sources.LocalSewerStudioDir,
                        Path.Combine("Einstellungen", "Local_SewerStudio"),
                        BackupExclusionRules.IsLocalSewerStudioDirExcluded,
                        Required: false),
                    new BackupSource(sources.RoamingSewerStudioDir,
                        Path.Combine("Einstellungen", "Roaming_SewerStudio"),
                        Required: false),
                    new BackupSource(sources.RoamingAuswertungProDir,
                        Path.Combine("Einstellungen", "Roaming_AuswertungPro"),
                        BackupExclusionRules.IsRoamingAuswertungProDirExcluded,
                        Required: false),
                }),

            new(
                "Logs",
                "Logdateien und Telemetrie",
                new[]
                {
                    new BackupSource(Path.Combine(sources.LocalSewerStudioDir, "logs"),
                        Path.Combine("Logs", "logs"),
                        Required: false),
                    new BackupSource(Path.Combine(sources.LocalSewerStudioDir, "Telemetry"),
                        Path.Combine("Logs", "Telemetry"),
                        Required: false),
                }),

            new(
                "Extras",
                "Desktop-Startskripte, Umgebungs-Snapshot, Wiederherstellungs-Anleitung",
                Array.Empty<BackupSource>(),
                BuildDesktopScriptFiles(sources.DesktopDir)),
        };

        return components;
    }

    private static IReadOnlyList<BackupSource> BuildProjectSources(
        IReadOnlyList<string>? requiredRoots,
        IReadOnlyList<string>? optionalRoots,
        bool includeVideos)
    {
        if ((requiredRoots is null || requiredRoots.Count == 0)
            && (optionalRoots is null || optionalRoots.Count == 0))
            return Array.Empty<BackupSource>();

        var normalized = new List<ProjectRootCandidate>();
        AddProjectRoots(normalized, requiredRoots, required: true);
        AddProjectRoots(normalized, optionalRoots, required: false);

        var selected = new List<ProjectRootCandidate>();
        foreach (var candidate in normalized
                     .OrderBy(item => item.Path.Length)
                     .ThenByDescending(item => item.Required)
                     .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            var parentIndex = selected.FindIndex(parent =>
                IsSameOrChildPath(parent.Path, candidate.Path));
            if (parentIndex < 0)
            {
                selected.Add(candidate);
                continue;
            }

            if (candidate.Required && !selected[parentIndex].Required)
            {
                selected[parentIndex] = selected[parentIndex] with { Required = true };
            }
        }

        var result = new List<BackupSource>();
        for (var index = 0; index < selected.Count; index++)
        {
            var candidate = selected[index];
            var leaf = SanitizeTargetSegment(Path.GetFileName(candidate.Path));
            if (string.IsNullOrWhiteSpace(leaf))
                leaf = "Projektwurzel";
            var target = Path.Combine("Projekte", $"{index + 1:00}_{leaf}");
            result.Add(new BackupSource(
                candidate.Path,
                target,
                IsDirExcluded: null,
                IsFileExcluded: includeVideos ? null : BackupExclusionRules.IsProjectVideoFileExcluded,
                Required: candidate.Required,
                WarnIfMissing: !candidate.Required));
        }

        return result;
    }

    private static void AddProjectRoots(
        List<ProjectRootCandidate> result,
        IReadOnlyList<string>? roots,
        bool required)
    {
        if (roots is null)
            return;

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            try
            {
                var full = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var existingIndex = result.FindIndex(item =>
                    string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase));
                if (existingIndex < 0)
                {
                    result.Add(new ProjectRootCandidate(full, required));
                }
                else if (required && !result[existingIndex].Required)
                {
                    result[existingIndex] = result[existingIndex] with { Required = true };
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ungueltige Settings-Pfade nicht in den Sicherungsplan aufnehmen.
            }
        }
    }

    private static bool IsSameOrChildPath(string parent, string candidate)
    {
        if (string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase))
            return true;
        var prefix = parent + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeTargetSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private static IReadOnlyList<BackupSingleFile> BuildDesktopScriptFiles(string desktopDir)
    {
        var files = new List<BackupSingleFile>();
        foreach (var name in DesktopScriptNames)
        {
            files.Add(new BackupSingleFile(
                Path.Combine(desktopDir, name),
                Path.Combine("Extras", name)));
        }
        return files;
    }

    private sealed record ProjectRootCandidate(string Path, bool Required);
}
