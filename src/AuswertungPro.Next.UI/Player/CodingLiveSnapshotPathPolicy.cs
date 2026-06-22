using System.IO;

namespace AuswertungPro.Next.UI.Player;

public static class CodingLiveSnapshotPathPolicy
{
    public static string BuildTempPath(Guid id)
        => Path.Combine(Path.GetTempPath(), $"coding_live_{id:N}.png");

    public static string CreateTempPath()
        => BuildTempPath(Guid.NewGuid());
}
