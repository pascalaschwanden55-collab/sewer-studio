using System.IO;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProjectFolderResolver
{
    public static string? ResolveNullable(string? projectPath)
        => string.IsNullOrWhiteSpace(projectPath)
            ? null
            : Path.GetDirectoryName(projectPath);

    public static string ResolveOrEmpty(string? projectPath)
        => ResolveNullable(projectPath) ?? string.Empty;
}
