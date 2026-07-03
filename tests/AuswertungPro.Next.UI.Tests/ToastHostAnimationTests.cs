using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ToastHostAnimationTests
{
    [Fact]
    public void Toast_Loaded_animiert_auch_eingefrorenen_translate_transform()
    {
        RunOnSta(() =>
        {
            var host = new ToastHost();
            var transform = new TranslateTransform { Y = 12d };
            transform.Freeze();
            var border = new Border { RenderTransform = transform };

            var exception = Record.Exception(() => InvokeToastLoaded(host, border));

            Assert.Null(exception);
            var mutableTransform = Assert.IsType<TranslateTransform>(border.RenderTransform);
            Assert.False(mutableTransform.IsFrozen);
        });
    }

    private static void InvokeToastLoaded(ToastHost host, Border border)
    {
        var method = typeof(ToastHost).GetMethod(
            "Toast_Loaded",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(host, [border, new RoutedEventArgs()]);
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
