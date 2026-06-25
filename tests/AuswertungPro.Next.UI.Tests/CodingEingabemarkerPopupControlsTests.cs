using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerPopupControlsTests
{
    [Fact]
    public void ShowInput_resets_input_and_selection_then_shows_popup()
    {
        RunOnStaThread(() =>
        {
            var popup = new Border { Visibility = Visibility.Collapsed };
            var text = new TextBox { Text = "alt" };
            var selection = new ComboBox { SelectedIndex = 1 };
            var show = FindMethod("ShowInput", typeof(FrameworkElement), typeof(TextBox), typeof(Selector));
            Assert.NotNull(show);

            show.Invoke(null, [popup, text, selection]);

            Assert.Equal(Visibility.Visible, popup.Visibility);
            Assert.Equal(string.Empty, text.Text);
            Assert.Equal(-1, selection.SelectedIndex);
        });
    }

    [Fact]
    public void Hide_collapses_popup()
    {
        RunOnStaThread(() =>
        {
            var popup = new Border { Visibility = Visibility.Visible };
            var hide = FindMethod("Hide", typeof(FrameworkElement));
            Assert.NotNull(hide);

            hide.Invoke(null, [popup]);

            Assert.Equal(Visibility.Collapsed, popup.Visibility);
        });
    }

    [Fact]
    public void IsVisible_returns_popup_visibility_state()
    {
        RunOnStaThread(() =>
        {
            var popup = new Border { Visibility = Visibility.Visible };
            var isVisible = FindMethod("IsVisible", typeof(FrameworkElement));
            Assert.NotNull(isVisible);

            Assert.True((bool)isVisible.Invoke(null, [popup])!);

            popup.Visibility = Visibility.Collapsed;

            Assert.False((bool)isVisible.Invoke(null, [popup])!);
        });
    }

    [Fact]
    public void ApplyQuickSelection_sets_input_text()
    {
        RunOnStaThread(() =>
        {
            var text = new TextBox { Text = "alt" };
            var apply = FindMethod("ApplyQuickSelection", typeof(TextBox), typeof(string));
            Assert.NotNull(apply);

            apply.Invoke(null, [text, "Riss bei 3 Uhr"]);

            Assert.Equal("Riss bei 3 Uhr", text.Text);
        });
    }

    [Fact]
    public void ResolveSelectedText_returns_combo_box_item_text_only()
    {
        RunOnStaThread(() =>
        {
            var resolve = FindMethod("ResolveSelectedText", typeof(object));
            Assert.NotNull(resolve);

            Assert.Equal("BAA", resolve.Invoke(null, [new ComboBoxItem { Content = "BAA" }]));
            Assert.Null(resolve.Invoke(null, [new ComboBoxItem { Content = "" }]));
            Assert.Null(resolve.Invoke(null, ["BAA"]));
            Assert.Null(resolve.Invoke(null, [null]));
        });
    }

    private static MethodInfo? FindMethod(string name, params Type[] parameterTypes)
        => typeof(PlayerStatusColors).Assembly
            .GetType("AuswertungPro.Next.UI.Views.Windows.CodingEingabemarkerPopupControls")
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
