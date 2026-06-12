using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolServerSecurityTests
{
    [Fact]
    public void ServerPy_schuetzt_Posts_MitTokenUndOriginCheck()
    {
        var server = File.ReadAllText(FindRepoFile("tools", "VideoLabelTool", "server.py"));

        Assert.Contains("MAX_POST_BYTES", server);
        Assert.Contains("X-Video-Label-Token", server);
        Assert.Contains("def require_post_auth", server);
        Assert.Contains("Origin", server);
        Assert.Contains("Referer", server);
        Assert.Contains("\"/session.json\"", server);
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
