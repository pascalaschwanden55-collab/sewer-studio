internal static class ModernizerFileSystem
{
    public static string? CopyFileToDirectory(
        string source,
        string targetDir,
        bool dryRun,
        ModernizeReport report,
        FileCopyKind kind)
    {
        var target = Path.Combine(targetDir, Path.GetFileName(source));
        return CopyFileExact(source, target, dryRun, report, kind);
    }

    public static string? CopyFileExact(
        string source,
        string target,
        bool dryRun,
        ModernizeReport report,
        FileCopyKind kind)
    {
        try
        {
            if (File.Exists(target))
            {
                if (SameFileContent(source, target))
                {
                    report.ReusedFiles++;
                    return target;
                }

                target = BuildCollisionSafePath(target);
            }

            if (!dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: false);
            }

            switch (kind)
            {
                case FileCopyKind.Import: report.ImportCopied++; break;
                case FileCopyKind.Haltung: report.HaltungFilesCopied++; break;
                case FileCopyKind.Schacht: report.SchachtFilesCopied++; break;
                case FileCopyKind.Plan: report.PlanFilesCopied++; break;
                case FileCopyKind.Photo: report.PhotoFilesCopied++; break;
            }

            return target;
        }
        catch (Exception ex)
        {
            report.CopyErrors++;
            report.Messages.Add($"Kopierfehler {source}: {ex.Message}");
            return null;
        }
    }

    public static string BuildCollisionSafePath(string target)
    {
        var dir = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{stem}_{i}{ext}");
            i++;
        }
        while (File.Exists(candidate));
        return candidate;
    }

    public static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            string[] files;
            try { files = Directory.GetFiles(dir); }
            catch { continue; }

            foreach (var file in files)
                yield return file;

            string[] dirs;
            try { dirs = Directory.GetDirectories(dir); }
            catch { continue; }

            foreach (var child in dirs)
                pending.Push(child);
        }
    }

    public static bool IsUnder(string path, string root)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;
            return full.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool SameFullPath(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }

    public static bool SameFileContent(string left, string right)
    {
        try
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (!leftInfo.Exists || !rightInfo.Exists || leftInfo.Length != rightInfo.Length)
                return false;

            using var leftStream = File.OpenRead(left);
            using var rightStream = File.OpenRead(right);
            var leftBuffer = new byte[81920];
            var rightBuffer = new byte[81920];

            while (true)
            {
                var leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
                var rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
                if (leftRead != rightRead)
                    return false;
                if (leftRead == 0)
                    return true;

                for (var i = 0; i < leftRead; i++)
                {
                    if (leftBuffer[i] != rightBuffer[i])
                        return false;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    public static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0L; }
    }
}
