internal static class ModernizerPathMap
{
    public static void Add(Dictionary<string, string> pathMap, string raw, string source, string relative)
    {
        pathMap[NormalizeKey(raw)] = relative;
        pathMap[NormalizeKey(source)] = relative;
    }

    public static bool TryGet(string raw, Dictionary<string, string> pathMap, out string relative)
        => pathMap.TryGetValue(NormalizeKey(raw), out relative!);

    private static string NormalizeKey(string path)
    {
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
        catch
        {
            return path.Trim().Replace('\\', '/');
        }
    }
}
