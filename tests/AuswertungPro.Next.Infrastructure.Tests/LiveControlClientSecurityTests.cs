namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class LiveControlClientSecurityTests
{
    [Fact]
    public void LiveControlClient_ValidatesLoopbackBeforeSendingToken()
    {
        var source = File.ReadAllText(FindRepoFile("tools", "SewerStudioMcpServer", "LiveControlClient.cs"));

        Assert.Contains("TryBuildLoopbackUrl", source);
        Assert.Contains("IPAddressLoopbackPolicy.IsLoopbackHost", source);
        Assert.Contains("localhost, 127.0.0.1 oder ::1", source);
        Assert.Contains("baseUri.Scheme is not (\"http\" or \"https\")", source);
        Assert.Contains("X-Live-Control-Token", source);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(SourceFilePath())! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
