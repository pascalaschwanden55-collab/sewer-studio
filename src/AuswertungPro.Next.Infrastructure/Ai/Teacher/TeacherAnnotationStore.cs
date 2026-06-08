using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Store fuer Lehrer-Annotationen (append-orientiert).
/// Hinzufuegen ist append-only; Loeschen NUR ueber DeleteAsync, das Load+Filter+Save
/// komplett innerhalb des Locks ausfuehrt. Thread-safe via SemaphoreSlim.
/// </summary>
public static class TeacherAnnotationStore
{
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string GetStorePath()
        => Path.Combine(KnowledgeBasePaths.GetRoot(), "teacher_annotations.json");

    /// <summary>Pfad zum Ordner fuer Lehrer-Bilder (Frames + Crops).</summary>
    public static string GetImagesDir()
    {
        var dir = Path.Combine(KnowledgeBasePaths.GetRoot(), "teacher_images");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Pfad zum Ordner fuer YOLO-Annotations (.txt).</summary>
    public static string GetLabelsDir()
    {
        var dir = Path.Combine(KnowledgeBasePaths.GetRoot(), "teacher_labels");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Alle gespeicherten Annotationen laden.</summary>
    public static async Task<List<TeacherAnnotation>> LoadAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            return await LoadInternalAsync();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Eine oder mehrere Annotationen hinzufuegen (append-only).
    /// Duplikat-Pruefung via AnnotationId.
    /// </summary>
    public static async Task AppendAsync(params TeacherAnnotation[] annotations)
    {
        if (annotations.Length == 0) return;

        await _fileLock.WaitAsync();
        try
        {
            var existing = await LoadInternalAsync();
            var existingIds = existing
                .Select(a => a.AnnotationId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var a in annotations)
            {
                if (existingIds.Contains(a.AnnotationId)) continue;
                existing.Add(a);
                existingIds.Add(a.AnnotationId);
            }

            await SaveInternalAsync(existing);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Eine Annotation per AnnotationId entfernen. Load+Filter+Save laufen KOMPLETT innerhalb
    /// von _fileLock (gleicher Lock wie AppendAsync) — kein Lost-Update gegen gleichzeitiges
    /// AppendAsync. Gibt true zurueck, wenn etwas entfernt wurde. (Audit R2)
    /// </summary>
    public static async Task<bool> DeleteAsync(string annotationId)
    {
        if (string.IsNullOrEmpty(annotationId)) return false;

        await _fileLock.WaitAsync();
        try
        {
            var existing = await LoadInternalAsync();
            var remaining = existing
                .Where(a => !string.Equals(a.AnnotationId, annotationId, StringComparison.Ordinal))
                .ToList();

            if (remaining.Count == existing.Count) return false;   // nichts entfernt

            await SaveInternalAsync(remaining);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>Anzahl gespeicherter Annotationen.</summary>
    public static async Task<int> CountAsync()
    {
        var list = await LoadAsync();
        return list.Count;
    }

    // ── Interne Methoden (ohne Lock, nur innerhalb von _fileLock aufrufen) ──

    private static async Task<List<TeacherAnnotation>> LoadInternalAsync()
    {
        var path = GetStorePath();
        if (!File.Exists(path)) return new List<TeacherAnnotation>();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<TeacherAnnotation>>(json, _jsonOpts)
                   ?? new List<TeacherAnnotation>();
        }
        catch (JsonException)
        {
            // Korrupte Datei NICHT still verschlucken: zur Forensik sichern, dann leer starten. (Audit R6)
            TryBackupCorrupt(path);
            return new List<TeacherAnnotation>();
        }
        catch
        {
            // Transienter IO-Fehler (z.B. kurz gesperrt) -> nicht als korrupt sichern.
            return new List<TeacherAnnotation>();
        }
    }

    private static void TryBackupCorrupt(string path)
    {
        try
        {
            File.Copy(path, path + ".corrupt", overwrite: true);
        }
        catch
        {
            // best effort
        }
    }

    private static async Task SaveInternalAsync(List<TeacherAnnotation> annotations)
    {
        var path = GetStorePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(annotations, _jsonOpts);

        // Atomar: erst vollstaendig in eine temp-Datei schreiben, dann per Verzeichnis-Swap
        // ersetzen. Ein Crash/Stromausfall waehrend des Schreibens beschaedigt so NIE die
        // Zieldatei (sie bleibt der alte, gueltige Stand). (Audit R6)
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json);

        if (File.Exists(path))
            File.Replace(tmp, path, path + ".bak");   // atomarer Swap + Vorgaenger nach .bak
        else
            File.Move(tmp, path);
    }
}
