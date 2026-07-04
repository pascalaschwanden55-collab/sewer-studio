using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Eine Spiegel-Quelle: Quellordner → Ziel-Relativpfad, optional mit Ordner-Ausschluss.
/// Das Praedikat bekommt den Ordnerpfad RELATIV zum SourceRoot (z. B. "src\Foo\bin").
/// </summary>
public sealed record BackupSource(
    string SourceRoot,
    string TargetRelativeRoot,
    Func<string, bool>? IsDirExcluded = null);

/// <summary>Eine einzelne Datei: Quellpfad → Ziel-Relativpfad (z. B. Desktop-Skripte).</summary>
public sealed record BackupSingleFile(string SourcePath, string TargetRelativePath);

/// <summary>Eine Backup-Komponente (Programm, KI-Gehirn, Einstellungen, Logs, Extras).</summary>
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
                "Einstellungen",
                "App-Einstellungen, Presets, Dropdowns, Preiskataloge, Vorlagen, Kataster-Tabelle",
                new[]
                {
                    new BackupSource(sources.LocalSewerStudioDir,
                        Path.Combine("Einstellungen", "Local_SewerStudio"),
                        BackupExclusionRules.IsLocalSewerStudioDirExcluded),
                    new BackupSource(sources.RoamingSewerStudioDir,
                        Path.Combine("Einstellungen", "Roaming_SewerStudio")),
                    new BackupSource(sources.RoamingAuswertungProDir,
                        Path.Combine("Einstellungen", "Roaming_AuswertungPro"),
                        BackupExclusionRules.IsRoamingAuswertungProDirExcluded),
                }),

            new(
                "Logs",
                "Logdateien und Telemetrie",
                new[]
                {
                    new BackupSource(Path.Combine(sources.LocalSewerStudioDir, "logs"),
                        Path.Combine("Logs", "logs")),
                    new BackupSource(Path.Combine(sources.LocalSewerStudioDir, "Telemetry"),
                        Path.Combine("Logs", "Telemetry")),
                }),

            new(
                "Extras",
                "Desktop-Startskripte, Umgebungs-Snapshot, Wiederherstellungs-Anleitung",
                Array.Empty<BackupSource>(),
                BuildDesktopScriptFiles(sources.DesktopDir)),
        };

        return components;
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
}
