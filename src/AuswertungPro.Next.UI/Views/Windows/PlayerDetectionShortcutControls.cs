using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class PlayerDetectionShortcutControls
{
    public static PlayerDetectionShortcutWorkflowActions CreateActions(
        ToggleButton codingLiveAiButton,
        ToggleButton liveDetectionButton,
        RoutedEventHandler codingLiveAiClick,
        RoutedEventHandler liveDetectionClick)
    {
        ArgumentNullException.ThrowIfNull(codingLiveAiButton);
        ArgumentNullException.ThrowIfNull(liveDetectionButton);
        ArgumentNullException.ThrowIfNull(codingLiveAiClick);
        ArgumentNullException.ThrowIfNull(liveDetectionClick);

        return new PlayerDetectionShortcutWorkflowActions(
            SetCodingLiveAiChecked: isChecked => codingLiveAiButton.IsChecked = isChecked,
            InvokeCodingLiveAi: () => codingLiveAiClick(codingLiveAiButton, new RoutedEventArgs()),
            SetLiveDetectionChecked: isChecked => liveDetectionButton.IsChecked = isChecked,
            InvokeLiveDetection: () => liveDetectionClick(liveDetectionButton, new RoutedEventArgs()));
    }
}
