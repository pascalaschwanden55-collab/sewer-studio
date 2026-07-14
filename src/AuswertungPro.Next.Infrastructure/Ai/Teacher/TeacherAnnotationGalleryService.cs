using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Datenlogik fuer die Lehrer-Annotationen-Galerie.
/// Haelt Store-/FewShot-/Dateioperationen aus dem WPF-Window heraus.
/// </summary>
public sealed class TeacherAnnotationGalleryService
{
    private const string AllFilter = "Alle";
    private const string TeacherSourcePrefix = "teacher:";
    private readonly ITeacherAnnotationStore _annotations;

    public TeacherAnnotationGalleryService()
        : this(TeacherAnnotationStore.Current)
    {
    }

    public TeacherAnnotationGalleryService(ITeacherAnnotationStore annotations)
    {
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
    }

    public async Task<TeacherAnnotationGallerySnapshot> LoadPendingAsync(CancellationToken ct = default)
    {
        var all = await _annotations.LoadAsync();
        var trainedIds = await LoadTrainedAnnotationIdsAsync(ct);
        var pending = trainedIds.Count > 0
            ? all.Where(a => !trainedIds.Contains(a.AnnotationId)).ToList()
            : all;

        return new TeacherAnnotationGallerySnapshot(
            PendingAnnotations: pending,
            FilterCodes: BuildFilterCodes(pending));
    }

    public static IReadOnlyList<string> BuildFilterCodes(IEnumerable<TeacherAnnotation> annotations)
        => annotations
            .Select(a => a.VsaCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<TeacherAnnotation> FilterByCode(
        IEnumerable<TeacherAnnotation> annotations,
        string? filterCode)
    {
        if (string.IsNullOrWhiteSpace(filterCode) ||
            string.Equals(filterCode, AllFilter, StringComparison.OrdinalIgnoreCase))
        {
            return annotations.ToList();
        }

        return annotations
            .Where(a => string.Equals(a.VsaCode, filterCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task DeleteAsync(TeacherAnnotation annotation)
    {
        TryDeleteFile(annotation.FullFramePath);
        TryDeleteFile(annotation.CroppedRegionPath);
        TryDeleteFile(annotation.YoloAnnotationPath);

        await _annotations.DeleteAsync(annotation.AnnotationId);
    }

    private static async Task<HashSet<string>> LoadTrainedAnnotationIdsAsync(CancellationToken ct)
    {
        try
        {
            var store = new FewShotExampleStore();
            await store.LoadAsync(ct);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var example in store.Examples)
            {
                if (example.Source.StartsWith(TeacherSourcePrefix, StringComparison.Ordinal))
                    ids.Add(example.Source[TeacherSourcePrefix.Length..]);
            }

            return ids;
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort: Store-Loeschen bleibt wichtiger als ein gesperrter Nebenpfad.
        }
    }
}

public sealed record TeacherAnnotationGallerySnapshot(
    IReadOnlyList<TeacherAnnotation> PendingAnnotations,
    IReadOnlyList<string> FilterCodes);
