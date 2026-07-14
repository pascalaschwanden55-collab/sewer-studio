namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Oeffnet eine Datei oder einen Ordner gezielt im Windows-Explorer.
/// </summary>
public interface IExplorerRevealService
{
    bool TryReveal(string? targetPath, out string? error);
}
