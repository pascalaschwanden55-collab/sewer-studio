using System.IO;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveSnapshotPathPolicyTests
{
    [Fact]
    public void BuildTempPath_uses_temp_directory_and_coding_live_prefix()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var path = CodingLiveSnapshotPathPolicy.BuildTempPath(id);

        Assert.Equal(
            Path.Combine(Path.GetTempPath(), "coding_live_11111111222233334444555555555555.png"),
            path);
    }

    [Fact]
    public void CreateTempPath_builds_unique_png_path_in_temp_directory()
    {
        var path = CodingLiveSnapshotPathPolicy.CreateTempPath();

        Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(path));
        Assert.StartsWith("coding_live_", Path.GetFileName(path), StringComparison.Ordinal);
        Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
    }
}
