using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolVisualStyleTests
{
    [Fact]
    public void AppHtml_nutzt_sewerstudio_style_tokens()
    {
        var html = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "app.html"));

        Assert.Contains("--bg:#0f172a", html);
        Assert.Contains("--card:#1e293b", html);
        Assert.Contains("--accent:#2563eb", html);
        Assert.Contains("--ok:#4ade80", html);
        Assert.Contains("--warn:#fbbf24", html);
        Assert.Contains("--danger:#ef4444", html);
        AssertNoForbiddenTokens(html, "--bg:#181818", "background:#101010");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene alte VideoLabelTool-Farben gefunden: " + string.Join(", ", hits));
    }
}
