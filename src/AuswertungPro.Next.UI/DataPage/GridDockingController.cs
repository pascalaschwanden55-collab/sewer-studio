using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.DataPage;

public static class GridDockingController
{
    public static UIElement ResolveActiveView(
        bool showDetailView,
        UIElement detailView,
        UIElement gridView)
        => showDetailView ? detailView : gridView;

    public static void ApplyUndockedState(
        Panel host,
        UIElement activeView,
        UIElement placeholder,
        Control undockButton,
        Control viewToggle)
    {
        host.Children.Remove(activeView);
        activeView.Visibility = Visibility.Visible;
        placeholder.Visibility = Visibility.Visible;
        undockButton.IsEnabled = false;
        viewToggle.IsEnabled = false;
    }

    public static bool RestoreDockedState(
        Panel host,
        UIElement? view,
        UIElement? fallbackView,
        UIElement placeholder,
        Control undockButton,
        Control viewToggle)
    {
        var element = view ?? fallbackView;
        var restored = false;
        if (element is not null)
        {
            if (!host.Children.Contains(element))
                host.Children.Add(element);

            element.Visibility = Visibility.Visible;
            restored = true;
        }

        placeholder.Visibility = Visibility.Collapsed;
        undockButton.IsEnabled = true;
        viewToggle.IsEnabled = true;
        return restored;
    }
}
