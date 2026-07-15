namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Loest Foto- und Videodateien aus einem VSA/XTF-Export auf.
/// </summary>
public interface IVsaMediaPathResolver
{
    string ResolvePhoto(string xtfPath, string? relativeFolder, string? fileName);

    string ResolveVideo(string xtfPath, string? relativeFolder, string? fileName);
}
