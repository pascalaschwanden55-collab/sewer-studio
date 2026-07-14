using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Ai.Backup;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Kennt ausschliesslich die Zuordnung zwischen lokalen Dateien und ZIP-Pfaden.
/// Export-/Importsteuerung und Nachbearbeitung bleiben ausserhalb.
/// </summary>
internal static class KnowledgeBackupFileCatalog
{
    public static IEnumerable<(string Source, string Entry)> EnumerateBackupFiles(
        KnowledgeBackupLocations locations)
    {
        var knowledgeRoot = locations.KnowledgeRoot;
        var kbDbPath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
        yield return (kbDbPath, "knowledge/KnowledgeBase.db");
        yield return (kbDbPath + "-wal", "knowledge/KnowledgeBase.db-wal");
        yield return (kbDbPath + "-shm", "knowledge/KnowledgeBase.db-shm");

        yield return (Path.Combine(knowledgeRoot, "training_samples.json"), "knowledge/training_samples.json");
        yield return (Path.Combine(knowledgeRoot, "training_settings.json"), "knowledge/training_settings.json");

        var knowledgeFramesDir = Path.Combine(knowledgeRoot, "frames");
        if (Directory.Exists(knowledgeFramesDir))
        {
            foreach (var png in Directory.EnumerateFiles(knowledgeFramesDir, "*.png"))
                yield return (png, "knowledge/frames/" + Path.GetFileName(png));
        }

        var goldLabelsDir = Path.Combine(knowledgeRoot, "gold_labels");
        if (Directory.Exists(goldLabelsDir))
        {
            foreach (var file in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(
                         goldLabelsDir,
                         "*.*",
                         recursive: true))
            {
                var relativePath = Path.GetRelativePath(goldLabelsDir, file).Replace('\\', '/');
                yield return (file, "knowledge/gold_labels/" + relativePath);
            }
        }

        yield return (Path.Combine(knowledgeRoot, "fewshot_examples.json"), "knowledge/fewshot_examples.json");
        var fewshotImagesDir = Path.Combine(knowledgeRoot, "fewshot_images");
        if (Directory.Exists(fewshotImagesDir))
        {
            foreach (var image in Directory.EnumerateFiles(fewshotImagesDir, "*.*"))
                yield return (image, "knowledge/fewshot_images/" + Path.GetFileName(image));
        }

        yield return (Path.Combine(knowledgeRoot, "teacher_annotations.json"), "knowledge/teacher_annotations.json");
        foreach (var item in EnumerateTree(
                     Path.Combine(knowledgeRoot, "teacher_images"),
                     "*.*",
                     "knowledge/teacher_images/"))
            yield return item;
        foreach (var item in EnumerateTree(
                     Path.Combine(knowledgeRoot, "teacher_labels"),
                     "*.txt",
                     "knowledge/teacher_labels/"))
            yield return item;

        yield return (Path.Combine(knowledgeRoot, "yolo_class_map.json"), "knowledge/yolo_class_map.json");
        yield return (Path.Combine(knowledgeRoot, "classes.txt"), "knowledge/classes.txt");
        yield return (Path.Combine(knowledgeRoot, "selftraining_history.json"), "knowledge/selftraining_history.json");
        yield return (Path.Combine(knowledgeRoot, "measures_learning.json"), "knowledge/measures_learning.json");
        yield return (Path.Combine(knowledgeRoot, "measures-model.zip"), "knowledge/measures-model.zip");
        yield return (locations.TrainingCenterStatePath, "knowledge/training_center.json");

        var legacyKbDir = Path.Combine(locations.RoamingAuswertungPro, "KiVideoanalyse");
        yield return (Path.Combine(legacyKbDir, "KnowledgeBase.db"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db");
        yield return (Path.Combine(legacyKbDir, "KnowledgeBase.db-wal"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db-wal");
        yield return (Path.Combine(legacyKbDir, "KnowledgeBase.db-shm"), "roaming_auswertungpro/KiVideoanalyse/KnowledgeBase.db-shm");
        yield return (Path.Combine(locations.RoamingAuswertungPro, "training_center_samples.json"), "roaming_auswertungpro/training_center_samples.json");
        yield return (Path.Combine(locations.RoamingAuswertungPro, "training_center_settings.json"), "roaming_auswertungpro/training_center_settings.json");
        yield return (Path.Combine(locations.RoamingAuswertungPro, "training_center.json"), "roaming_auswertungpro/training_center.json");

        var legacyFramesDir = Path.Combine(locations.RoamingAuswertungPro, "frames");
        if (Directory.Exists(legacyFramesDir))
        {
            foreach (var png in Directory.EnumerateFiles(legacyFramesDir, "*.png"))
                yield return (png, "roaming_auswertungpro/frames/" + Path.GetFileName(png));
        }

        var dropdownsDir = Path.Combine(locations.RoamingSewerStudio, "dropdowns");
        if (Directory.Exists(dropdownsDir))
        {
            foreach (var json in Directory.EnumerateFiles(dropdownsDir, "*.json"))
                yield return (json, "roaming_sewerstudio/dropdowns/" + Path.GetFileName(json));
        }

        yield return (Path.Combine(locations.RoamingSewerStudio, "presets.json"), "roaming_sewerstudio/presets.json");
        yield return (Path.Combine(locations.LocalSewerStudio, "settings.json"), "local_sewerstudio/settings.json");
    }

    public static string? MapEntryToLocalPath(
        string entryName,
        KnowledgeBackupLocations locations)
        => KnowledgeBackupPathMapper.MapEntryToLocalPath(
            entryName,
            knowledgeRoot: locations.KnowledgeRoot,
            roamingAp: locations.RoamingAuswertungPro,
            roamingSs: locations.RoamingSewerStudio,
            localSs: locations.LocalSewerStudio);

    private static IEnumerable<(string Source, string Entry)> EnumerateTree(
        string root,
        string searchPattern,
        string entryPrefix)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var file in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(
                     root,
                     searchPattern,
                     recursive: true))
        {
            var relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
            yield return (file, entryPrefix + relativePath);
        }
    }
}
