using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Common;

public sealed class FolderOpenService : IFolderOpenService
{
    private readonly ISafeShellOpenService _shellOpen;

    public FolderOpenService(ISafeShellOpenService shellOpen)
    {
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
    }

    public FolderOpenResult EnsureAndOpen(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new FolderOpenResult(false, "Pfad fehlt.");

        try
        {
            Directory.CreateDirectory(path);
            return _shellOpen.TryOpen(path, out var error)
                ? new FolderOpenResult(true, null)
                : new FolderOpenResult(false, error ?? "Unbekannter Fehler");
        }
        catch (Exception ex)
        {
            return new FolderOpenResult(false, ex.Message);
        }
    }
}
