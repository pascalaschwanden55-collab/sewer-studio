internal static class ModernizerPathComparison
{
    public static bool PathValueEquals(string left, string right)
        => string.Equals(left.Trim().Replace('\\', '/'), right.Trim().Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    public static bool PathEquals(string left, string right)
    {
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch { return string.Equals(left, right, StringComparison.OrdinalIgnoreCase); }
    }

    public static bool IsDirectChildFile(string file, string folder)
    {
        try
        {
            return File.Exists(file)
                   && PathEquals(Path.GetDirectoryName(Path.GetFullPath(file)) ?? "", Path.GetFullPath(folder));
        }
        catch
        {
            return false;
        }
    }

    public static IEnumerable<string> DeduplicatePathValues(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var key = value.Trim().Replace('\\', '/');
            if (seen.Add(key))
                yield return value;
        }
    }

    public static bool DeduplicatePathValuesInPlace(List<string> values)
    {
        var deduped = DeduplicatePathValues(values).ToList();
        if (deduped.Count == values.Count)
            return false;

        values.Clear();
        values.AddRange(deduped);
        return true;
    }
}
