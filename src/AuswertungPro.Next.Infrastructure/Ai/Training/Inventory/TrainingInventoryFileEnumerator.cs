namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

/// <summary>
/// Durchsucht nur echte Ordner. Links und Junctions werden bewusst nicht verfolgt,
/// damit geschuetzte Daten nicht ueber einen Alias als Reparaturkandidat erscheinen.
/// </summary>
internal static class TrainingInventoryFileEnumerator
{
    public static IEnumerable<string> EnumerateTopLevelFiles(
        string root,
        ICollection<string> skippedPaths,
        CancellationToken cancellationToken)
    {
        if (!TryReadAttributes(root, skippedPaths, out var rootAttributes)
            || (rootAttributes & FileAttributes.Directory) == 0
            || (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            yield break;
        }

        if (!TryReadDirectory(root, skippedPaths, out var files, out _))
            yield break;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryReadAttributes(file, skippedPaths, out var attributes)
                || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            yield return file;
        }
    }

    public static IEnumerable<string> EnumerateFiles(
        string root,
        ICollection<string> skippedPaths,
        CancellationToken cancellationToken)
    {
        if (!TryReadAttributes(root, skippedPaths, out var rootAttributes)
            || (rootAttributes & FileAttributes.Directory) == 0
            || (rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            yield break;
        }

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            if (!TryReadAttributes(current, skippedPaths, out var currentAttributes)
                || (currentAttributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            if (!TryReadDirectory(current, skippedPaths, out var files, out var directories))
                continue;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadAttributes(file, skippedPaths, out var attributes)
                    || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                yield return file;
            }

            for (var index = directories.Length - 1; index >= 0; index--)
            {
                var directory = directories[index];
                if (!TryReadAttributes(directory, skippedPaths, out var attributes)
                    || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                stack.Push(directory);
            }
        }
    }

    private static bool TryReadDirectory(
        string path,
        ICollection<string> skippedPaths,
        out string[] files,
        out string[] directories)
    {
        try
        {
            files = Directory.EnumerateFiles(path)
                .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            directories = Directory.EnumerateDirectories(path)
                .OrderBy(candidate => candidate, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skippedPaths.Add(path);
            files = [];
            directories = [];
            return false;
        }
    }

    private static bool TryReadAttributes(
        string path,
        ICollection<string> skippedPaths,
        out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                skippedPaths.Add(path);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            skippedPaths.Add(path);
            attributes = default;
            return false;
        }
    }
}
