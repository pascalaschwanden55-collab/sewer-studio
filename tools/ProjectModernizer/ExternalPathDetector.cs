using System.Text.RegularExpressions;

internal static class ExternalPathDetector
{
    public static bool IsExternalAbsolutePath(string value, string projectFolder)
    {
        var trimmed = value.Trim();
        if (!Path.IsPathRooted(trimmed))
            return false;

        return !ModernizerFileSystem.IsUnder(trimmed, projectFolder);
    }

    public static bool ContainsExternalDrivePath(string value, string projectFolder)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var matches = Regex.Matches(value, @"[A-Za-z]:[\\/][^|;\r\n]+");
        foreach (Match match in matches)
        {
            var candidate = match.Value.Trim();
            if (Path.IsPathRooted(candidate) && !ModernizerFileSystem.IsUnder(candidate, projectFolder))
                return true;
        }

        return false;
    }
}
