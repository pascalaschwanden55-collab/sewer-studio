using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingOverlayInputControls
{
    public static void ApplyActiveToolSelection(
        TextBlock activeToolLabel,
        ButtonBase createEventButton,
        string labelText)
    {
        ArgumentNullException.ThrowIfNull(activeToolLabel);
        ArgumentNullException.ThrowIfNull(createEventButton);

        activeToolLabel.Text = labelText;
        createEventButton.IsEnabled = false;
    }

    public static void SetCreateEventEnabled(ButtonBase createEventButton, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(createEventButton);

        createEventButton.IsEnabled = isEnabled;
    }
}
