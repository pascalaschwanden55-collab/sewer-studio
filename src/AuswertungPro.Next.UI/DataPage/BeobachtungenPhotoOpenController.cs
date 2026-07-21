using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.UI.DataPage;

public enum BeobachtungenPhotoOpenStatus
{
    Ignored,
    NotFound,
    Opened,
    OpenFailed
}

public sealed record BeobachtungenPhotoOpenResult(
    BeobachtungenPhotoOpenStatus Status,
    string? Error = null);

public sealed class BeobachtungenPhotoOpenController
{
    private readonly IInspectionProtocolFileLocator _inspectionProtocolFiles;
    private readonly ISafeShellOpenService _shellOpen;

    public BeobachtungenPhotoOpenController(
        IInspectionProtocolFileLocator inspectionProtocolFiles,
        ISafeShellOpenService shellOpen)
    {
        _inspectionProtocolFiles = inspectionProtocolFiles
            ?? throw new ArgumentNullException(nameof(inspectionProtocolFiles));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
    }

    public BeobachtungenPhotoOpenResult Open(string? rawPath, string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return new(BeobachtungenPhotoOpenStatus.Ignored);

        var resolvedPath = _inspectionProtocolFiles.ResolveExistingPath(rawPath, projectPath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return new(BeobachtungenPhotoOpenStatus.NotFound);

        return _shellOpen.TryOpen(resolvedPath, out var error)
            ? new(BeobachtungenPhotoOpenStatus.Opened)
            : new(BeobachtungenPhotoOpenStatus.OpenFailed, error);
    }
}
