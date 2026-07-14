namespace AuswertungPro.Next.Application.Common;

public sealed record FolderOpenResult(bool Success, string? Error);

public interface IFolderOpenService
{
    FolderOpenResult EnsureAndOpen(string? path);
}
