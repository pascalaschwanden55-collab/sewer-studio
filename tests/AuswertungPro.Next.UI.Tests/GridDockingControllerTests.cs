using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class GridDockingControllerTests
{
    [Fact]
    public void ResolveActiveView_selects_detail_view_only_when_toggle_is_checked()
    {
        RunOnSta(() =>
        {
            var table = new Grid();
            var details = new Border();

            Assert.Same(details, GridDockingController.ResolveActiveView(showDetailView: true, details, table));
            Assert.Same(table, GridDockingController.ResolveActiveView(showDetailView: false, details, table));
        });
    }

    [Fact]
    public void ApplyUndockedState_removes_active_view_and_disables_docking_controls()
    {
        RunOnSta(() =>
        {
            var host = new Grid();
            var activeView = new Border { Visibility = Visibility.Collapsed };
            var placeholder = new Border { Visibility = Visibility.Collapsed };
            var undockButton = new Button { IsEnabled = true };
            var viewToggle = new CheckBox { IsEnabled = true };
            host.Children.Add(activeView);

            GridDockingController.ApplyUndockedState(
                host,
                activeView,
                placeholder,
                undockButton,
                viewToggle);

            Assert.Empty(host.Children.OfType<UIElement>());
            Assert.Equal(Visibility.Visible, activeView.Visibility);
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
            Assert.False(undockButton.IsEnabled);
            Assert.False(viewToggle.IsEnabled);
        });
    }

    [Fact]
    public void RestoreDockedState_adds_view_once_and_enables_docking_controls()
    {
        RunOnSta(() =>
        {
            var host = new Grid();
            var restoredView = new Border { Visibility = Visibility.Collapsed };
            var placeholder = new Border { Visibility = Visibility.Visible };
            var undockButton = new Button { IsEnabled = false };
            var viewToggle = new CheckBox { IsEnabled = false };

            var restored = GridDockingController.RestoreDockedState(
                host,
                view: restoredView,
                fallbackView: null,
                placeholder,
                undockButton,
                viewToggle);
            var restoredAgain = GridDockingController.RestoreDockedState(
                host,
                view: restoredView,
                fallbackView: null,
                placeholder,
                undockButton,
                viewToggle);

            Assert.True(restored);
            Assert.True(restoredAgain);
            Assert.Single(host.Children.OfType<UIElement>());
            Assert.Same(restoredView, Assert.Single(host.Children.OfType<UIElement>()));
            Assert.Equal(Visibility.Visible, restoredView.Visibility);
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            Assert.True(undockButton.IsEnabled);
            Assert.True(viewToggle.IsEnabled);
        });
    }

    [Fact]
    public void RestoreDockedState_uses_fallback_view_and_still_resets_controls_without_view()
    {
        RunOnSta(() =>
        {
            var host = new Grid();
            var fallbackView = new Border();
            var placeholder = new Border { Visibility = Visibility.Visible };
            var undockButton = new Button { IsEnabled = false };
            var viewToggle = new CheckBox { IsEnabled = false };

            var restoredFallback = GridDockingController.RestoreDockedState(
                host,
                view: null,
                fallbackView,
                placeholder,
                undockButton,
                viewToggle);
            var restoredNothing = GridDockingController.RestoreDockedState(
                host,
                view: null,
                fallbackView: null,
                placeholder,
                undockButton,
                viewToggle);

            Assert.True(restoredFallback);
            Assert.False(restoredNothing);
            Assert.Contains(fallbackView, host.Children.OfType<UIElement>());
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            Assert.True(undockButton.IsEnabled);
            Assert.True(viewToggle.IsEnabled);
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
