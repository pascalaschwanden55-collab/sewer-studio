using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI;

public enum FluentBackdrop
{
    None,
    Mica
}

public static class Fluent
{
    public static readonly DependencyProperty BackdropProperty =
        DependencyProperty.RegisterAttached(
            "Backdrop",
            typeof(FluentBackdrop),
            typeof(Fluent),
            new PropertyMetadata(FluentBackdrop.None, OnBackdropChanged));

    public static void SetBackdrop(DependencyObject element, FluentBackdrop value)
        => element.SetValue(BackdropProperty, value);

    public static FluentBackdrop GetBackdrop(DependencyObject element)
        => (FluentBackdrop)element.GetValue(BackdropProperty);

    private static void OnBackdropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
            return;

        if (window.IsLoaded)
        {
            WindowBackdropHelper.Apply(window, ThemeManager.CurrentTheme, GetBackdrop(window) == FluentBackdrop.Mica);
            return;
        }

        window.Loaded += ApplyOnLoaded;
    }

    private static void ApplyOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window)
            return;

        window.Loaded -= ApplyOnLoaded;
        WindowBackdropHelper.Apply(window, ThemeManager.CurrentTheme, GetBackdrop(window) == FluentBackdrop.Mica);
    }
}
