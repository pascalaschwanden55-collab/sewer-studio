using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Media;

public sealed class PhotoImportResult
{
    public List<string> ImportedRelativePaths { get; } = new();
    public List<string> UnassignedRelativePaths { get; } = new();
}

public interface IPhotoImportService
{
    PhotoImportResult ImportFolderToProjectMedia(
        string folderPath,
        string projectMediaRootAbs,
        ProtocolRevision revision);
}
