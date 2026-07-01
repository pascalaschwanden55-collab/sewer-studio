using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;
using Xunit;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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

            Assert.DoesNotContain(activeView, host.Children.OfType<UIElement>());
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

    [Fact]
    public void DataPage_grid_docking_state_uses_controller()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var dockingBlock = ExtractBetween(
            source,
            "private void UndockGrid()",
            "private void BeobachtungenMenu_Click");

        Assert.Contains("GridDockingController.ResolveActiveView", dockingBlock);
        Assert.Contains("GridDockingController.ApplyUndockedState", dockingBlock);
        Assert.Contains("GridDockingController.RestoreDockedState", dockingBlock);
        Assert.DoesNotContain("GridHost.Children.Remove(active);", dockingBlock);
        Assert.DoesNotContain("UndockedPlaceholder.Visibility = Visibility.Collapsed;", dockingBlock);
        Assert.DoesNotContain("UndockButton.IsEnabled = true;", dockingBlock);
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

    private static string ExtractBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker not found: {startMarker}");
        Assert.True(end > start, $"End marker not found: {endMarker}");
        return source[start..end];
    }
}
