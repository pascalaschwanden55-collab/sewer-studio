namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Liefert die fuer Protokoll-PDFs benoetigten Logo- und Fotodateien.
/// Die Application-Schicht beschreibt nur den Vertrag; der Dateizugriff liegt in Infrastructure.
/// </summary>
public interface IProtocolPdfAssetResolver
{
    byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs);

    IReadOnlyList<string> ResolvePhotoPaths(
        IReadOnlyList<string> photoPaths,
        string projectRootAbs,
        int maxPhotos,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null);

    string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null);

    byte[]? ReadAllBytes(string path);
}
