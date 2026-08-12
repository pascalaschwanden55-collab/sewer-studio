using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Dateibasierter Speicher fuer Lehrer-Annotationen. Append und Delete verwenden denselben
/// Instanz-Lock, damit parallele Aenderungen nicht verloren gehen.
/// </summary>
public sealed class TeacherAnnotationFileStore : ITeacherAnnotationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Func<string> _rootDirectoryProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public TeacherAnnotationFileStore()
        : this(() => KnowledgeBasePaths.GetRoot())
    {
    }

    public TeacherAnnotationFileStore(string rootDirectory)
        : this(() => rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Der Stammordner darf nicht leer sein.", nameof(rootDirectory));
    }

    internal TeacherAnnotationFileStore(Func<string> rootDirectoryProvider)
    {
        _rootDirectoryProvider = rootDirectoryProvider
            ?? throw new ArgumentNullException(nameof(rootDirectoryProvider));
    }

    public string StoragePath => Path.Combine(RootDirectory, "teacher_annotations.json");

    private string RootDirectory => Path.GetFullPath(_rootDirectoryProvider());

    public string GetImagesDir() => EnsureDirectory("teacher_images");

    public string GetLabelsDir() => EnsureDirectory("teacher_labels");

    public async Task<List<TeacherAnnotation>> LoadAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await LoadInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AppendAsync(params TeacherAnnotation[] annotations)
    {
        if (annotations.Length == 0)
            return;

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var existing = await LoadInternalAsync().ConfigureAwait(false);
            var existingIds = existing
                .Select(annotation => annotation.AnnotationId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var annotation in annotations)
            {
                if (existingIds.Contains(annotation.AnnotationId))
                    continue;

                existing.Add(annotation);
                existingIds.Add(annotation.AnnotationId);
            }

            await SaveInternalAsync(existing).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string annotationId)
    {
        if (string.IsNullOrEmpty(annotationId))
            return false;

        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var existing = await LoadInternalAsync().ConfigureAwait(false);
            var remaining = existing
                .Where(annotation => !string.Equals(
                    annotation.AnnotationId,
                    annotationId,
                    StringComparison.Ordinal))
                .ToList();

            if (remaining.Count == existing.Count)
                return false;

            await SaveInternalAsync(remaining).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<int> CountAsync() =>
        (await LoadAsync().ConfigureAwait(false)).Count;

    private async Task<List<TeacherAnnotation>> LoadInternalAsync()
    {
        var path = StoragePath;
        if (!File.Exists(path))
            return new List<TeacherAnnotation>();

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<TeacherAnnotation>>(json, JsonOptions)
                   ?? throw new JsonException(
                       "Lehrer-Annotationen muessen als JSON-Liste gespeichert sein.");
        }
        catch (JsonException ex)
        {
            // Beweissicherung vor dem Wurf — danach wird nichts mehr geschrieben.
            BestEffort.Try(
                () => File.Copy(path, path + ".corrupt", overwrite: true),
                "Lehrer-Annotationen: korrupte Datei sichern");
            throw new InvalidOperationException(
                $"Die Lehrer-Annotationen sind beschaedigt ({path}); der Bestand wurde "
                + $"NICHT veraendert — eine Sicherungskopie liegt als .corrupt daneben, "
                + $"und es wird nichts gespeichert. {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // Unlesbar ist ein Fehler, kein Erstlauf: Eine voruebergehende Sperre darf
            // den Bestand nicht durch den naechsten Speichervorgang loeschen. Dieselbe
            // Regel wie beim Gold-Store (AP-1) und den Kostendateien (CostStoreFileProbe):
            // fehlend = leer, unlesbar = Fehler.
            throw new InvalidOperationException(
                $"Die Lehrer-Annotationen sind nicht lesbar ({path}). Der vorhandene "
                + $"Bestand wurde NICHT veraendert — es wird nichts gespeichert. "
                + $"{ex.GetType().Name}: {ex.Message}", ex);
        }
    }

    private async Task SaveInternalAsync(List<TeacherAnnotation> annotations)
    {
        var json = JsonSerializer.Serialize(annotations, JsonOptions);
        await AtomicTextFileWriter
            .WriteAllTextAsync(StoragePath, json)
            .ConfigureAwait(false);
    }

    private string EnsureDirectory(string name)
    {
        var directory = Path.Combine(RootDirectory, name);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
