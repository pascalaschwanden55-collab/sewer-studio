using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ITeacherAnnotationStore"/>.
/// </summary>
public static class TeacherAnnotationStore
{
    private static ITeacherAnnotationStore _current = new TeacherAnnotationFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ITeacherAnnotationStore Current => Volatile.Read(ref _current);

    /// <summary>Verbindet die Fassade mit der zentral aufgebauten Dienstinstanz.</summary>
    public static void Use(ITeacherAnnotationStore store) =>
        Volatile.Write(ref _current, store ?? throw new ArgumentNullException(nameof(store)));

    public static string GetImagesDir() => Current.GetImagesDir();

    public static string GetLabelsDir() => Current.GetLabelsDir();

    public static Task<List<TeacherAnnotation>> LoadAsync() => Current.LoadAsync();

    public static Task AppendAsync(params TeacherAnnotation[] annotations) =>
        Current.AppendAsync(annotations);

    public static Task<bool> DeleteAsync(string annotationId) =>
        Current.DeleteAsync(annotationId);

    public static Task<int> CountAsync() => Current.CountAsync();
}
