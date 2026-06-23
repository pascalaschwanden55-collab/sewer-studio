using System;
using System.IO;

namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerVideoPathInfo(string VideoPath, string DisplayName);

public static class PlayerVideoPathGuard
{
    public static PlayerVideoPathInfo Validate(string? videoPath, Func<string, bool>? fileExists = null)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            throw new FileNotFoundException("Video nicht gefunden", videoPath);

        fileExists ??= File.Exists;
        if (!fileExists(videoPath))
            throw new FileNotFoundException("Video nicht gefunden", videoPath);

        var fileName = Path.GetFileName(videoPath);
        var displayName = string.IsNullOrWhiteSpace(fileName) ? "Video" : fileName;
        return new PlayerVideoPathInfo(videoPath, displayName);
    }
}
