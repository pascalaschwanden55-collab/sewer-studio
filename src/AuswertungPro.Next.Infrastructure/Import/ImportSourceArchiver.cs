using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Ergebnis eines Archive-Aufrufs.
/// </summary>
/// <param name="Copied">Anzahl der neu kopierten Dateien.</param>
/// <param name="Reused">Anzahl der unveraendert schon vorhandenen Dateien (Idempotenz).</param>
/// <param name="Messages">Hinweise, z. B. bei Namenskollisionen durch abweichende Dateigroesse.</param>
public sealed record ArchiveResult(int Copied, int Reused, IReadOnlyList<string> Messages);

/// <summary>
/// Kopiert Rohdaten aus einem Kanalfernsehen-Quellordner in die normierten
/// Importdateien-Unterordner des Projekts.
/// Mapping: .fdb/.db3/.mdb → Datenbanken | .xtf → XTF | .pdf → PDF | .txt → TXT.
/// Videos und Bilddateien werden NICHT kopiert.
/// Idempotent: gleicher Dateiname + gleiche Groesse → Reuse (kein erneutes Kopieren).
/// Abweichende Groesse → kollisionssicherer Name + Hinweismeldung.
/// </summary>
public static class ImportSourceArchiver
{
    // Mapping von Dateiendung (Kleinbuchstaben) auf Unterordner-Konstante
    private static readonly Dictionary<string, string> EndungsMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".fdb",  ProjectStructure.Datenbanken },
        { ".db3",  ProjectStructure.Datenbanken },
        { ".mdb",  ProjectStructure.Datenbanken },
        { ".xtf",  ProjectStructure.XtfDir },
        { ".m150", ProjectStructure.XtfDir },
        { ".xml",  ProjectStructure.XtfDir },
        { ".pdf",  ProjectStructure.PdfDir },
        { ".txt",  ProjectStructure.TxtDir },
    };

    /// <summary>
    /// Archiviert alle relevanten Dateien aus <paramref name="sourceFolder"/> (rekursiv)
    /// in die Importdateien-Unterordner von <paramref name="projectFolder"/>.
    /// </summary>
    /// <param name="sourceFolder">Absoluter Pfad zum Quellordner (Kanalfernsehen-Export).</param>
    /// <param name="projectFolder">Absoluter Pfad zum Projektstammordner.</param>
    public static ArchiveResult Archive(string sourceFolder, string projectFolder)
    {
        var copied   = 0;
        var reused   = 0;
        var messages = new List<string>();

        // Alle Dateien rekursiv enumerieren; gesperrte Unterordner werden uebersprungen
        foreach (var sourcePath in SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*", recursive: true))
        {
            try
            {
                ArchiveOne(sourcePath, projectFolder, ref copied, ref reused, messages);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
            {
                messages.Add($"Archivkopie fehlgeschlagen, weitere Dateien werden verarbeitet: {sourcePath} ({ex.Message})");
            }
        }

        return new ArchiveResult(copied, reused, messages);
    }

    private static void ArchiveOne(
        string sourcePath,
        string projectFolder,
        ref int copied,
        ref int reused,
        List<string> messages)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!EndungsMapping.TryGetValue(extension, out var subKind))
            return;

        var targetDir = ProjectStructure.ImportdateienDir(projectFolder, subKind);
        Directory.CreateDirectory(targetDir);

        var fileName = Path.GetFileName(sourcePath);
        var targetPath = Path.Combine(targetDir, fileName);
        if (!File.Exists(targetPath))
        {
            CopyAtomically(sourcePath, targetPath);
            copied++;
            return;
        }

        var sourceSize = new FileInfo(sourcePath).Length;
        var targetSize = new FileInfo(targetPath).Length;
        if (sourceSize == targetSize)
        {
            reused++;
            return;
        }

        var safeName = BaueKollisionssicherenNamen(targetDir, fileName);
        CopyAtomically(sourcePath, Path.Combine(targetDir, safeName));
        copied++;
        messages.Add(
            $"Namenskollision: '{fileName}' im Ziel hat abweichende Groesse " +
            $"({targetSize} vs. {sourceSize} Bytes). Kopiert als '{safeName}'.");
    }

    private static void CopyAtomically(string sourcePath, string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            File.Move(tempPath, targetPath, overwrite: false);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Ermittelt einen nicht kollidierenden Dateinamen im Zielordner.
    /// Schema: &lt;Basisname&gt;_1.ext, _2.ext, usw.
    /// </summary>
    private static string BaueKollisionssicherenNamen(string targetDir, string originalName)
    {
        var baseName  = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        var counter   = 1;

        string candidate;
        do
        {
            candidate = $"{baseName}_{counter}{extension}";
            counter++;
        }
        while (File.Exists(Path.Combine(targetDir, candidate)));

        return candidate;
    }
}
