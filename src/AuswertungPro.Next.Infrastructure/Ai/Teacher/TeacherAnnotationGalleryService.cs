using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Datenlogik fuer die Lehrer-Annotationen-Galerie.
/// Haelt Store- und Dateioperationen aus dem WPF-Window heraus.
/// </summary>
public sealed class TeacherAnnotationGalleryService
{
    private const string AllFilter = "Alle";
    private readonly ITeacherAnnotationStore _annotations;

    public TeacherAnnotationGalleryService()
        : this(TeacherAnnotationStore.Current)
    {
    }

    public TeacherAnnotationGalleryService(ITeacherAnnotationStore annotations)
    {
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
    }

    public async Task<TeacherAnnotationGallerySnapshot> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var annotations = await _annotations.LoadAsync();
        ct.ThrowIfCancellationRequested();

        return new TeacherAnnotationGallerySnapshot(
            Annotations: annotations,
            FilterCodes: BuildFilterCodes(annotations));
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
    IReadOnlyList<TeacherAnnotation> Annotations,
    IReadOnlyList<string> FilterCodes);
