using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Reine Ausschluss-Regeln der Datensicherung (kein IO).
/// Ausgeschlossen wird nur Regenerierbares bzw. bewusst ausgelassener Altbestand —
/// alles andere wird gesichert (Default-Include, sichere Richtung).
/// </summary>
public static class BackupExclusionRules
{
    private static readonly HashSet<string> ProjectVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".m4v", ".ts", ".mts", ".m2ts"
    };
    // Build-/Tool-Artefakte im Repo: aus Quellcode jederzeit neu erzeugbar.
    private static readonly string[] ProgramExcludedNames =
        { "bin", "obj", ".vs", "node_modules", ".venv", "venv", "__pycache__", ".pytest_cache", ".pytest_tmp" };

    // Regenerierbare Brocken im KI-Gehirn (mit Nutzer geklaert, 2026-07-03):
    // Trainings-Datensaetze und alte KB-Zwischensicherungen lassen sich neu bauen.
    private static readonly string[] KiBrainExcludedNames =
        { "training_frames", "kb_backups" };

    // Oberste Ebene %LOCALAPPDATA%\SewerStudio: "Knowledge" ist Altbestand
    // (das echte Gehirn wird als eigene Komponente gesichert), logs/Telemetry
    // wandern in die Logs-Komponente.
    private static readonly string[] LocalSewerStudioTopLevelExcluded =
        { "Knowledge", "logs", "Telemetry" };

    // Oberste Ebene %APPDATA%\AuswertungPro: frames/yolo_dataset sind Altbestand
    // von vor dem Umzug nach C:\KI_BRAIN.
    private static readonly string[] RoamingAuswertungProTopLevelExcluded =
        { "frames", "yolo_dataset" };

    /// <summary>Programm-Komponente: Ordnername auf beliebiger Tiefe (.git bleibt bewusst DRIN).</summary>
    public static bool IsProgramDirExcluded(string relativeDirPath)
        => NameMatches(LastSegment(relativeDirPath), ProgramExcludedNames);

    /// <summary>KI-Gehirn-Komponente: Ordnername auf beliebiger Tiefe.</summary>
    public static bool IsKiBrainDirExcluded(string relativeDirPath)
    {
        var name = LastSegment(relativeDirPath);
        return MatchesYoloDatasetPattern(name) || NameMatches(name, KiBrainExcludedNames);
    }

    /// <summary>
    /// Muster fuer regenerierbare YOLO-Trainingsdatensaetze:
    /// beginnt mit "yolo_" UND enthaelt "dataset" (z. B. yolo_vsa_cls_dataset_v2_bal).
    /// "yolo_models" oder "yolodataset" matchen NICHT.
    /// </summary>
    public static bool MatchesYoloDatasetPattern(string dirName)
        => dirName.StartsWith("yolo_", StringComparison.OrdinalIgnoreCase)
           && dirName.Contains("dataset", StringComparison.OrdinalIgnoreCase);

    /// <summary>%LOCALAPPDATA%\SewerStudio: Ausschluss NUR auf oberster Ebene.</summary>
    public static bool IsLocalSewerStudioDirExcluded(string relativeDirPath)
        => IsTopLevel(relativeDirPath)
           && NameMatches(relativeDirPath, LocalSewerStudioTopLevelExcluded);

    /// <summary>%APPDATA%\AuswertungPro: Ausschluss NUR auf oberster Ebene.</summary>
    public static bool IsRoamingAuswertungProDirExcluded(string relativeDirPath)
        => IsTopLevel(relativeDirPath)
           && NameMatches(relativeDirPath, RoamingAuswertungProTopLevelExcluded);

    public static bool IsProjectVideoFileExcluded(string relativeFilePath)
        => ProjectVideoExtensions.Contains(Path.GetExtension(relativeFilePath));

    private static bool IsTopLevel(string relativeDirPath)
        => !relativeDirPath.Contains(Path.DirectorySeparatorChar)
           && !relativeDirPath.Contains(Path.AltDirectorySeparatorChar);

    private static string LastSegment(string relativeDirPath)
    {
        var trimmed = relativeDirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private static bool NameMatches(string name, string[] candidates)
        => candidates.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
}
