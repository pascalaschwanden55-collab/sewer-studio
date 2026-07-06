internal static class ModernizerEmptyDirectoryCleaner
{
    public static bool TryDeleteDirectoryTreeIfEmpty(string root, bool dryRun)
    {
        try
        {
            if (!Directory.Exists(root))
                return false;

            if (Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any())
                return false;

            if (!dryRun)
                Directory.Delete(root, recursive: true);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
