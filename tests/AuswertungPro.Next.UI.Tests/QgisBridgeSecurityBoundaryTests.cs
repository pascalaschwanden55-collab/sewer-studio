using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class QgisBridgeSecurityBoundaryTests
{
    [Fact]
    public void Bridge_is_documented_and_guarded_as_local_read_only_single_user_feed()
    {
        var bridgeServer = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "QgisBridge", "QgisBridgeServer.cs"));
        var liveControl = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.UI", "LiveControl", "LiveControlServer.cs"));
        var readme = File.ReadAllText(
            RepoFile("integrations", "qgis", "README.md"));

        Assert.Contains("new TcpListener(IPAddress.Loopback", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("method is not (\"GET\" or \"HEAD\")", bridgeServer, StringComparison.Ordinal);
        Assert.Contains("request.Method == \"GET\"", liveControl, StringComparison.Ordinal);

        Assert.Contains("Einzelplatz", readme, StringComparison.Ordinal);
        Assert.Contains("kein Token", readme, StringComparison.Ordinal);
        Assert.Contains("Mehrbenutzer", readme, StringComparison.Ordinal);
    }
}
