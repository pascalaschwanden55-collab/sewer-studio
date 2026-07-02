using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoLabelToolServerSecurityTests
{
    [Fact]
    public void ServerPy_schuetzt_Posts_MitTokenUndOriginCheck()
    {
        var server = File.ReadAllText(RepoFile("tools", "VideoLabelTool", "server.py"));

        Assert.Contains("MAX_POST_BYTES", server);
        Assert.Contains("X-Video-Label-Token", server);
        Assert.Contains("def require_post_auth", server);
        Assert.Contains("Origin", server);
        Assert.Contains("Referer", server);
        Assert.Contains("\"/session.json\"", server);
    }

}
