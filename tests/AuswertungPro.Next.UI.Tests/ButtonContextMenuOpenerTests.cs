using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Views.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ButtonContextMenuOpenerTests
{
    [Fact]
    public void OpenFromButton_opens_attached_context_menu_below_button()
    {
        RunOnSta(() =>
        {
            var dataContext = new object();
            var menu = new ContextMenu();
            var button = new Button { ContextMenu = menu };

            var opened = ButtonContextMenuOpener.OpenFromButton(button, dataContext);

            Assert.True(opened);
            Assert.Same(button, menu.PlacementTarget);
            Assert.Equal(PlacementMode.Bottom, menu.Placement);
            Assert.Same(dataContext, menu.DataContext);
            Assert.True(menu.IsOpen);

            menu.IsOpen = false;
        });
    }

    [Fact]
    public void OpenFromButton_ignores_non_buttons_and_buttons_without_menu()
    {
        RunOnSta(() =>
        {
            Assert.False(ButtonContextMenuOpener.OpenFromButton(new object(), dataContext: null));
            Assert.False(ButtonContextMenuOpener.OpenFromButton(new Button(), dataContext: null));
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
