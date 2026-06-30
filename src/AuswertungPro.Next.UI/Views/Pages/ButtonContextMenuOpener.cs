using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class ButtonContextMenuOpener
{
    public static bool OpenFromButton(object sender, object? dataContext)
    {
        if (sender is not Button button || button.ContextMenu is null)
            return false;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.DataContext = dataContext;
        button.ContextMenu.IsOpen = true;
        return true;
    }
}
