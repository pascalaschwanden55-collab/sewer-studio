using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Phase 3.3: PageHeader-Control fuer einheitliches Page-Header-Layout.
/// Title (Pflicht) + Subtitle (optional) + Actions-Slot (optional, rechts).
/// </summary>
public partial class PageHeader : UserControl
{
    public PageHeader()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PageHeader),
            new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle), typeof(string), typeof(PageHeader),
            new PropertyMetadata(string.Empty, OnSubtitleChanged));

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty HasSubtitleProperty =
        DependencyProperty.Register(
            nameof(HasSubtitle), typeof(bool), typeof(PageHeader),
            new PropertyMetadata(false));

    public bool HasSubtitle
    {
        get => (bool)GetValue(HasSubtitleProperty);
        private set => SetValue(HasSubtitleProperty, value);
    }

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(
            nameof(Actions), typeof(object), typeof(PageHeader),
            new PropertyMetadata(null));

    /// <summary>Optionaler Inhalt rechts im Header (z.B. Buttons).</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageHeader header)
            header.HasSubtitle = !string.IsNullOrWhiteSpace(e.NewValue as string);
    }
}
