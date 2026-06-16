using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Common;

/// <summary>
/// Compatibility facade for callers that still use the Infrastructure namespace.
/// The implementation lives in Application.Common so Application-layer services can use it too.
/// </summary>
public static class SafeFileEnumeration
{
    public static IEnumerable<string> EnumerateDirectoriesSafe(
        string root,
        ICollection<string>? skippedDirectories = null)
        => AuswertungPro.Next.Application.Common.SafeFileEnumeration.EnumerateDirectoriesSafe(root, skippedDirectories);

    public static IEnumerable<string> EnumerateFilesSafe(
        string root,
        string searchPattern = "*",
        bool recursive = true,
        ICollection<string>? skippedDirectories = null)
        => AuswertungPro.Next.Application.Common.SafeFileEnumeration.EnumerateFilesSafe(root, searchPattern, recursive, skippedDirectories);
}
