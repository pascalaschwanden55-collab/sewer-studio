using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventListItemSelectionHelperTests
{
    [Fact]
    public void SelectContainingListBoxItem_selects_direct_list_box_item()
    {
        RunOnStaThread(() =>
        {
            var item = new ListBoxItem();
            var method = FindSelectMethod();
            Assert.NotNull(method);

            var selected = method.Invoke(null, [item]);

            Assert.Equal(true, selected);
            Assert.True(item.IsSelected);
        });
    }

    [Fact]
    public void SelectContainingListBoxItem_returns_false_without_item()
    {
        RunOnStaThread(() =>
        {
            var method = FindSelectMethod();
            Assert.NotNull(method);

            var selected = method.Invoke(null, [new TextBlock()]);

            Assert.Equal(false, selected);
        });
    }

    [Fact]
    public void SelectContainingListBoxItem_returns_false_for_null_source()
    {
        RunOnStaThread(() =>
        {
            var method = FindSelectMethod();
            Assert.NotNull(method);

            var selected = method.Invoke(null, [null]);

            Assert.Equal(false, selected);
        });
    }

    private static Type? HelperType
        => typeof(CodingDefectStatusDisplayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Coding.CodingEventListItemSelectionHelper");

    private static MethodInfo? FindSelectMethod()
        => HelperType?.GetMethod(
            "SelectContainingListBoxItem",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(DependencyObject)],
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
