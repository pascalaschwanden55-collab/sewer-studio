using System.Windows;

namespace AuswertungPro.Next.UI;

public sealed class RichToolTipContent : Freezable
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(RichToolTipContent), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(RichToolTipContent), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(RichToolTipContent), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShortcutProperty =
        DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(RichToolTipContent), new PropertyMetadata(string.Empty));

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Shortcut
    {
        get => (string)GetValue(ShortcutProperty);
        set => SetValue(ShortcutProperty, value);
    }

    protected override Freezable CreateInstanceCore()
        => new RichToolTipContent();
}
