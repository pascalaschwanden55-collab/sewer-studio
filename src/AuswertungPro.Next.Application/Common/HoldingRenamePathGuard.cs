namespace AuswertungPro.Next.Application.Common;

/// <summary>Prueft die Arbeitskopien vor einer Haltungsumbenennung und vor jedem Move.</summary>
internal sealed class HoldingRenamePathGuard(string? projectFilePath)
{
    internal string EnsureSafePath(string path)
        => ProjectPathResolver.EnsureWritableProjectPath(path, projectFilePath);

    internal void EnsureSafeTree(string folder)
    {
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var current))
        {
            current = EnsureSafePath(current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var safe = EnsureSafePath(entry);
                if ((File.GetAttributes(safe) & FileAttributes.Directory) != 0)
                    pending.Push(safe);
            }
        }
    }
}
