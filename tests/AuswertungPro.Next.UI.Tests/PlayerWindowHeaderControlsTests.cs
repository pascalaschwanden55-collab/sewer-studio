using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowHeaderControlsTests
{
    [Fact]
    public void ApplyVideoInfo_sets_title_name_and_path()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();
            var nameText = new TextBlock();
            var pathText = new TextBlock();
            var info = new PlayerVideoPathInfo(@"C:\videos\pipe.mp4", "pipe.mp4");
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [window, nameText, pathText, info]);

            Assert.Equal("Video - pipe.mp4", window.Title);
            Assert.Equal("pipe.mp4", nameText.Text);
            Assert.Equal(@"C:\videos\pipe.mp4", pathText.Text);
        });
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(PlayerVideoPathGuard).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerWindowHeaderControls")
            ?.GetMethod(
                "ApplyVideoInfo",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Window), typeof(TextBlock), typeof(TextBlock), typeof(PlayerVideoPathInfo)],
                modifiers: null);

    private static void RunOnStaThread(Action action)
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
            throw exception;
    }
}
