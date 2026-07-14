namespace AuswertungPro.Next.Application.Common;

public interface ISafeShellOpenService
{
    bool TryOpen(string? path, out string? error);
}
