using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolMatchHighlightControlsTests
{
    [Fact]
    public void Apply_sets_container_highlight_and_badge()
    {
        RunOnStaThread(() =>
        {
            var container = new ListBoxItem();
            var badge = new Border { Visibility = Visibility.Collapsed };
            var badgeText = new TextBlock();
            var apply = FindMethod(
                "Apply",
                typeof(ListBoxItem),
                typeof(Border),
                typeof(TextBlock),
                typeof(CodingProtocolMatchBucket));
            Assert.NotNull(apply);

            apply.Invoke(null, [container, badge, badgeText, CodingProtocolMatchBucket.TrainingGreen]);

            Assert.Equal(
                CodingProtocolMatchDisplayPolicy.BackgroundColor(CodingProtocolMatchBucket.TrainingGreen),
                Assert.IsType<SolidColorBrush>(container.Background).Color);
            Assert.Equal(CodingProtocolMatchDisplayPolicy.Tooltip(CodingProtocolMatchBucket.TrainingGreen), container.ToolTip);
            Assert.Equal(
                CodingProtocolMatchDisplayPolicy.BadgeColor(CodingProtocolMatchBucket.TrainingGreen),
                Assert.IsType<SolidColorBrush>(badge.Background).Color);
            Assert.Equal(Visibility.Visible, badge.Visibility);
            Assert.Equal(CodingProtocolMatchDisplayPolicy.BadgeText(CodingProtocolMatchBucket.TrainingGreen), badgeText.Text);
        });
    }

    [Fact]
    public void Clear_removes_container_highlight_and_hides_badge()
    {
        RunOnStaThread(() =>
        {
            var container = new ListBoxItem
            {
                Background = Brushes.Red,
                ToolTip = "alt"
            };
            var badge = new Border { Visibility = Visibility.Visible };
            var clear = FindMethod("Clear", typeof(ListBoxItem), typeof(Border));
            Assert.NotNull(clear);

            clear.Invoke(null, [container, badge]);

            Assert.Null(container.ReadLocalValue(Control.BackgroundProperty) as Brush);
            Assert.Equal(DependencyProperty.UnsetValue, container.ReadLocalValue(FrameworkElement.ToolTipProperty));
            Assert.Equal(Visibility.Collapsed, badge.Visibility);
        });
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(CodingProtocolMatchDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingProtocolMatchHighlightControls")
            ?.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: parameterTypes,
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
