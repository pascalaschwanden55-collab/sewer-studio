using System.Windows;

namespace AuswertungPro.Next.UI.Settings;

public static class SettingsNavigation
{
    public static readonly DependencyProperty GroupProperty =
        DependencyProperty.RegisterAttached(
            "Group",
            typeof(string),
            typeof(SettingsNavigation),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty HintProperty =
        DependencyProperty.RegisterAttached(
            "Hint",
            typeof(string),
            typeof(SettingsNavigation),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty IndexProperty =
        DependencyProperty.RegisterAttached(
            "Index",
            typeof(string),
            typeof(SettingsNavigation),
            new FrameworkPropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsGroupStartProperty =
        DependencyProperty.RegisterAttached(
            "IsGroupStart",
            typeof(bool),
            typeof(SettingsNavigation),
            new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasGroupDividerProperty =
        DependencyProperty.RegisterAttached(
            "HasGroupDivider",
            typeof(bool),
            typeof(SettingsNavigation),
            new FrameworkPropertyMetadata(false));

    public static string GetGroup(DependencyObject element)
        => (string)element.GetValue(GroupProperty);

    public static void SetGroup(DependencyObject element, string value)
        => element.SetValue(GroupProperty, value);

    public static string GetHint(DependencyObject element)
        => (string)element.GetValue(HintProperty);

    public static void SetHint(DependencyObject element, string value)
        => element.SetValue(HintProperty, value);

    public static string GetIndex(DependencyObject element)
        => (string)element.GetValue(IndexProperty);

    public static void SetIndex(DependencyObject element, string value)
        => element.SetValue(IndexProperty, value);

    public static bool GetIsGroupStart(DependencyObject element)
        => (bool)element.GetValue(IsGroupStartProperty);

    public static void SetIsGroupStart(DependencyObject element, bool value)
        => element.SetValue(IsGroupStartProperty, value);

    public static bool GetHasGroupDivider(DependencyObject element)
        => (bool)element.GetValue(HasGroupDividerProperty);

    public static void SetHasGroupDivider(DependencyObject element, bool value)
        => element.SetValue(HasGroupDividerProperty, value);
}
