using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

internal static class ArchitectureSourceGuard
{
    public static string[] FindPlayerWindowPartialTokenOffenders(params string[] forbiddenTokens)
        => FindWindowTokenOffenders("PlayerWindow*.cs", forbiddenTokens);

    public static string[] FindDataPagePartialTokenOffenders(params string[] forbiddenTokens)
    {
        var pagesRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages");
        return Directory.EnumerateFiles(pagesRoot, "DataPage*.cs")
            .SelectMany(path => FindFileTokenOffenders(path, forbiddenTokens))
            .ToArray();
    }

    public static string[] FindFileTokenOffenders(string path, params string[] forbiddenTokens)
    {
        var source = File.ReadAllText(path);
        var tokens = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        return tokens.Length == 0
            ? []
            : [$"{Path.GetFileName(path)}: {string.Join(", ", tokens)}"];
    }

    public static string[] FindWindowTokenOffenders(string searchPattern, params string[] forbiddenTokens)
    {
        var windowsRoot = RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows");

        return Directory.EnumerateFiles(windowsRoot, searchPattern)
            .Select(path =>
            {
                var source = File.ReadAllText(path);
                return new
                {
                    File = Path.GetFileName(path),
                    Tokens = forbiddenTokens
                        .Where(token => source.Contains(token, StringComparison.Ordinal))
                        .ToArray()
                };
            })
            .Where(item => item.Tokens.Length > 0)
            .Select(item => $"{item.File}: {string.Join(", ", item.Tokens)}")
            .ToArray();
    }
}
