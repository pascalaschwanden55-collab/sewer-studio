using System.Text.Json;

namespace AuswertungPro.Next.Domain.Models;

public static class PdfCorrectionMetadata
{
    public const string HoldingRenameMapKey = "PDF_HoldingRenameMap";
    public const string ShaftRenameMapKey = "PDF_ShaftRenameMap";

    public static bool RegisterHoldingRename(Project? project, string? oldValue, string? newValue)
        => RegisterRename(project, HoldingRenameMapKey, oldValue, newValue);

    public static bool RegisterShaftRename(Project? project, string? oldValue, string? newValue)
        => RegisterRename(project, ShaftRenameMapKey, oldValue, newValue);

    public static string ResolveHolding(Project? project, string? value)
        => ResolveValue(project, HoldingRenameMapKey, value);

    public static string ResolveShaft(Project? project, string? value)
        => ResolveValue(project, ShaftRenameMapKey, value);

    public static IReadOnlyDictionary<string, string> LoadHoldingRenames(Project? project)
        => LoadMap(project, HoldingRenameMapKey);

    public static IReadOnlyDictionary<string, string> LoadShaftRenames(Project? project)
        => LoadMap(project, ShaftRenameMapKey);

    private static bool RegisterRename(Project? project, string metadataKey, string? oldValue, string? newValue)
    {
        if (project is null)
            return false;

        var oldToken = NormalizeToken(oldValue);
        var newToken = NormalizeToken(newValue);
        if (string.IsNullOrWhiteSpace(oldToken)
            || string.IsNullOrWhiteSpace(newToken)
            || string.Equals(oldToken, newToken, StringComparison.OrdinalIgnoreCase))
            return false;

        var map = LoadMutableMap(project, metadataKey);
        var changed = false;

        if (map.TryGetValue(newToken, out var reverseTarget)
            && string.Equals(NormalizeToken(reverseTarget), oldToken, StringComparison.OrdinalIgnoreCase))
        {
            map.Remove(newToken);
            changed = true;
        }

        if (!map.TryGetValue(oldToken, out var existingTarget)
            || !string.Equals(NormalizeToken(existingTarget), newToken, StringComparison.OrdinalIgnoreCase))
        {
            map[oldToken] = newToken;
            changed = true;
        }

        if (NormalizeMap(map))
            changed = true;

        if (!changed)
            return false;

        SaveMap(project, metadataKey, map);
        return true;
    }

    private static string ResolveValue(Project? project, string metadataKey, string? value)
    {
        var token = NormalizeToken(value);
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var map = LoadMap(project, metadataKey);
        if (map.Count == 0)
            return token;

        return ResolveValue(map, token);
    }

    private static IReadOnlyDictionary<string, string> LoadMap(Project? project, string metadataKey)
        => LoadMutableMap(project, metadataKey);

    private static Dictionary<string, string> LoadMutableMap(Project? project, string metadataKey)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (project is null
            || !project.Metadata.TryGetValue(metadataKey, out var raw)
            || string.IsNullOrWhiteSpace(raw))
            return map;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            if (parsed is null)
                return map;

            foreach (var kv in parsed)
            {
                var key = NormalizeToken(kv.Key);
                var value = NormalizeToken(kv.Value);
                if (string.IsNullOrWhiteSpace(key)
                    || string.IsNullOrWhiteSpace(value)
                    || string.Equals(key, value, StringComparison.OrdinalIgnoreCase))
                    continue;

                map[key] = value;
            }

            NormalizeMap(map);
            return map;
        }
        catch
        {
            return map;
        }
    }

    private static void SaveMap(Project project, string metadataKey, IReadOnlyDictionary<string, string> map)
    {
        if (map.Count == 0)
            project.Metadata.Remove(metadataKey);
        else
            project.Metadata[metadataKey] = JsonSerializer.Serialize(map);

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
    }

    private static bool NormalizeMap(Dictionary<string, string> map)
    {
        var changed = false;
        var keys = map.Keys.ToList();

        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var targetRaw))
                continue;

            var target = NormalizeToken(targetRaw);
            if (string.IsNullOrWhiteSpace(target))
            {
                map.Remove(key);
                changed = true;
                continue;
            }

            var resolved = ResolveValue(map, target);
            if (string.IsNullOrWhiteSpace(resolved)
                || string.Equals(key, resolved, StringComparison.OrdinalIgnoreCase))
            {
                map.Remove(key);
                changed = true;
                continue;
            }

            if (!string.Equals(target, resolved, StringComparison.OrdinalIgnoreCase))
            {
                map[key] = resolved;
                changed = true;
            }
        }

        return changed;
    }

    private static string ResolveValue(IReadOnlyDictionary<string, string> map, string value)
    {
        var current = NormalizeToken(value);
        if (string.IsNullOrWhiteSpace(current))
            return string.Empty;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (map.TryGetValue(current, out var nextRaw))
        {
            var next = NormalizeToken(nextRaw);
            if (string.IsNullOrWhiteSpace(next)
                || string.Equals(current, next, StringComparison.OrdinalIgnoreCase)
                || !visited.Add(current))
                break;

            current = next;
        }

        return current;
    }

    private static string NormalizeToken(string? value)
        => (value ?? string.Empty).Trim();
}
