namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class LiveControlClientSecurityTests
{
    [Fact]
    public void LiveControlClient_ValidatesLoopbackBeforeSendingToken()
    {
        var source = File.ReadAllText(RepoFile("tools", "SewerStudioMcpServer", "LiveControlClient.cs"));

        Assert.Contains("TryBuildLoopbackUrl", source);
        Assert.Contains("IPAddressLoopbackPolicy.IsLoopbackHost", source);
        Assert.Contains("localhost, 127.0.0.1 oder ::1", source);
        Assert.Contains("baseUri.Scheme is not (\"http\" or \"https\")", source);
        Assert.Contains("X-Live-Control-Token", source);
    }
}
