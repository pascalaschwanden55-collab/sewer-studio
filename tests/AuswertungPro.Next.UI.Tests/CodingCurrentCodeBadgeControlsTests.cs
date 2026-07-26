using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCurrentCodeBadgeControlsTests
{
    [Fact]
    public void Apply_shows_badge_text_when_state_is_visible()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBlock();
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [badge, text, new CodingCurrentCodeBadgeState(true, "1.00m BBA")]);

            Assert.Equal("1.00m BBA", text.Text);
            Assert.Equal(Visibility.Visible, badge.Visibility);
        });
    }

    [Fact]
    public void Apply_hides_badge_and_clears_text_when_state_is_hidden()
    {
        RunOnStaThread(() =>
        {
            var badge = new Border { Visibility = Visibility.Visible };
            var text = new TextBlock { Text = "old" };
            var apply = FindApplyMethod();
            Assert.NotNull(apply);

            apply.Invoke(null, [badge, text, CodingCurrentCodeBadgeState.Hidden]);

            Assert.Equal("", text.Text);
            Assert.Equal(Visibility.Collapsed, badge.Visibility);
        });
    }

    private static MethodInfo? FindApplyMethod()
        => typeof(CodingCurrentCodeBadgePolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingCurrentCodeBadgeControls")
            ?.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Border), typeof(TextBlock), typeof(CodingCurrentCodeBadgeState)],
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
