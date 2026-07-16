using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die Dateiarbeit liegt im
/// <see cref="ITeacherAnnotationStore"/>.
/// </summary>
public static class TeacherAnnotationStore
{
    private static readonly ITeacherAnnotationStore Default = new TeacherAnnotationFileStore();

    public static string DefaultPath => Current.StoragePath;

    public static ITeacherAnnotationStore Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ITeacherAnnotationStore store) =>
        throw new NotSupportedException(
            "Die globale Ablage fuer Lehrer-Annotationen kann nicht mehr ausgetauscht werden. " +
            "ITeacherAnnotationStore bitte per Konstruktor uebergeben.");

    public static string GetImagesDir() => Current.GetImagesDir();

    public static string GetLabelsDir() => Current.GetLabelsDir();

    public static Task<List<TeacherAnnotation>> LoadAsync() => Current.LoadAsync();

    public static Task AppendAsync(params TeacherAnnotation[] annotations) =>
        Current.AppendAsync(annotations);

    public static Task<bool> DeleteAsync(string annotationId) =>
        Current.DeleteAsync(annotationId);

    public static Task<int> CountAsync() => Current.CountAsync();
}
