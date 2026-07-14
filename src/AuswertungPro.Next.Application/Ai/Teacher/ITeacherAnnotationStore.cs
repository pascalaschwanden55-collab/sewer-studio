namespace AuswertungPro.Next.Application.Ai.Teacher;

/// <summary>
/// Persistiert manuell bestaetigte Lehrer-Annotationen und stellt ihre Bildordner bereit.
/// </summary>
public interface ITeacherAnnotationStore
{
    string StoragePath { get; }

    string GetImagesDir();

    string GetLabelsDir();

    Task<List<TeacherAnnotation>> LoadAsync();

    Task AppendAsync(params TeacherAnnotation[] annotations);

    Task<bool> DeleteAsync(string annotationId);

    Task<int> CountAsync();
}
