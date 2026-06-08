using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolVisualStyleTests
{
    [Fact]
    public void AppHtml_nutzt_sewerstudio_style_tokens()
    {
        var html = File.ReadAllText(FindRepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("--bg:#0f172a", html);
        Assert.Contains("--card:#1e293b", html);
        Assert.Contains("--accent:#2563eb", html);
        Assert.Contains("--ok:#4ade80", html);
        Assert.Contains("--warn:#fbbf24", html);
        Assert.Contains("--danger:#ef4444", html);
        Assert.DoesNotContain("--bg:#181818", html);
        Assert.DoesNotContain("background:#101010", html);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }
}
