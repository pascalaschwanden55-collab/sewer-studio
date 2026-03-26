using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Teacher;

/// <summary>
/// Append-only Store fuer Lehrer-Annotationen.
/// Jede Speicherung erzeugt einen neuen Datensatz — kein Update, kein Delete.
/// Thread-safe via SemaphoreSlim.
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
        => Path.Combine(KnowledgeRoot.GetRoot(), "teacher_annotations.json");

    /// <summary>Pfad zum Ordner fuer Lehrer-Bilder (Frames + Crops).</summary>
    public static string GetImagesDir()
    {
        var dir = Path.Combine(KnowledgeRoot.GetRoot(), "teacher_images");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Pfad zum Ordner fuer YOLO-Annotations (.txt).</summary>
    public static string GetLabelsDir()
    {
        var dir = Path.Combine(KnowledgeRoot.GetRoot(), "teacher_labels");
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
        catch
        {
            return new List<TeacherAnnotation>();
        }
    }

    private static async Task SaveInternalAsync(List<TeacherAnnotation> annotations)
    {
        var path = GetStorePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(annotations, _jsonOpts);
        await File.WriteAllTextAsync(path, json);
    }
}
