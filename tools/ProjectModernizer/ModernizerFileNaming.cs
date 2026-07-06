using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

internal static class ModernizerFileNaming
{
    public static string ResolveDateStamp(HaltungRecord record)
    {
        var raw = record.GetFieldValue(ModernizerProjectKeys.InspectionDateYear)?.Trim();
        if (TryResolveDateStamp(raw, out var stamp))
            return stamp;

        foreach (var field in ModernizerRecordFieldSpecs.HaltungDateStampFields.Select(spec => spec.Field))
        {
            if (TryExtractDateStampFromPath(record.GetFieldValue(field), out stamp))
                return stamp;
        }

        return "00000000";
    }

    public static string ResolveDateStampFromFolder(string folder)
    {
        foreach (var file in ModernizerFileSystem.EnumerateFilesSafe(folder))
        {
            if (TryExtractDateStampFromPath(file, out var stamp))
                return stamp;
        }

        return "00000000";
    }

    public static bool HasDpMarker(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Contains("_DP", StringComparison.OrdinalIgnoreCase)
               || stem.Contains("-DP", StringComparison.OrdinalIgnoreCase)
               || stem.EndsWith("DP", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasEigenMarker(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return stem.Contains("_E", StringComparison.OrdinalIgnoreCase)
               || stem.Contains("-E", StringComparison.OrdinalIgnoreCase)
               || stem.Contains("Eigen", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveDateStamp(string? raw, out string stamp)
    {
        stamp = "00000000";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("de-CH"), DateTimeStyles.None, out var d)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
        {
            stamp = d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return true;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 4)
        {
            stamp = digits + "0101";
            return true;
        }

        if (digits.Length >= 8)
        {
            var candidate = digits[..8];
            if (DateTime.TryParseExact(candidate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                stamp = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractDateStampFromPath(string? path, out string stamp)
    {
        stamp = "00000000";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var match = Regex.Match(path, @"(?<!\d)(?:19|20)\d{6}(?!\d)");
        if (!match.Success)
            return false;

        stamp = match.Value;
        return true;
    }
}
