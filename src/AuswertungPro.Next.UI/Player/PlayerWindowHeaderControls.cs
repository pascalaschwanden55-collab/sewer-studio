using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerWindowHeaderControls
{
    public static void ApplyVideoInfo(
        Window window,
        TextBlock videoNameText,
        TextBlock videoPathText,
        PlayerVideoPathInfo videoInfo)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(videoNameText);
        ArgumentNullException.ThrowIfNull(videoPathText);
        ArgumentNullException.ThrowIfNull(videoInfo);

        window.Title = $"Video - {videoInfo.DisplayName}";
        videoNameText.Text = videoInfo.DisplayName;
        videoPathText.Text = videoInfo.VideoPath;
    }
}
