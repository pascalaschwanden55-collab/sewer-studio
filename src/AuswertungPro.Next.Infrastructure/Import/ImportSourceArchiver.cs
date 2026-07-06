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
            var extension = Path.GetExtension(sourcePath);

            // Nicht gemappte Endungen (Videos, Bilder usw.) ignorieren
            if (!EndungsMapping.TryGetValue(extension, out var subKind))
                continue;

            // Zielordner sicherstellen
            var targetDir = ProjectStructure.ImportdateienDir(projectFolder, subKind);
            Directory.CreateDirectory(targetDir);

            var fileName   = Path.GetFileName(sourcePath);
            var targetPath = Path.Combine(targetDir, fileName);

            if (File.Exists(targetPath))
            {
                // Groesse vergleichen fuer Idempotenz-Pruefung
                var sourceSize = new FileInfo(sourcePath).Length;
                var targetSize = new FileInfo(targetPath).Length;

                if (sourceSize == targetSize)
                {
                    // Identisch: Reuse, kein erneutes Kopieren
                    reused++;
                }
                else
                {
                    // Abweichende Groesse: kollisionssicheren Namen vergeben
                    var safeName = BaueKollisionssicherenNamen(targetDir, fileName);
                    var safePath = Path.Combine(targetDir, safeName);
                    File.Copy(sourcePath, safePath, overwrite: false);
                    copied++;
                    messages.Add(
                        $"Namenskollision: '{fileName}' im Ziel hat abweichende Groesse " +
                        $"({targetSize} vs. {sourceSize} Bytes). Kopiert als '{safeName}'.");
                }
            }
            else
            {
                // Neu kopieren
                File.Copy(sourcePath, targetPath, overwrite: false);
                copied++;
            }
        }

        return new ArchiveResult(copied, reused, messages);
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
