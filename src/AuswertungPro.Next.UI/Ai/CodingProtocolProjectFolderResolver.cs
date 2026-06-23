using System.IO;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingProtocolProjectFolderResolver
{
    public static string? Resolve(string? projectPath)
        => string.IsNullOrWhiteSpace(projectPath)
            ? null
            : Path.GetDirectoryName(projectPath);
}
