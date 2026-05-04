using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Controls;

/// <summary>
/// Phase 3.3: StatusBadge-Control fuer farbige Status-Anzeige.
/// Variant: Success | Warning | Danger | Info | Muted.
/// </summary>
public partial class StatusBadge : UserControl
{
    public StatusBadge()
    {
        InitializeComponent();
        UpdateColors();
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(StatusBadge),
            new PropertyMetadata(string.Empty));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant), typeof(StatusBadgeVariant), typeof(StatusBadge),
            new PropertyMetadata(StatusBadgeVariant.Info, OnVariantChanged));

    public StatusBadgeVariant Variant
    {
        get => (StatusBadgeVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public static readonly DependencyProperty BackgroundBrushProperty =
        DependencyProperty.Register(
            nameof(BackgroundBrush), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(null));

    public Brush? BackgroundBrush
    {
        get => (Brush?)GetValue(BackgroundBrushProperty);
        private set => SetValue(BackgroundBrushProperty, value);
    }

    public static readonly DependencyProperty ForegroundBrushProperty =
        DependencyProperty.Register(
            nameof(ForegroundBrush), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(null));

    public Brush? ForegroundBrush
    {
        get => (Brush?)GetValue(ForegroundBrushProperty);
        private set => SetValue(ForegroundBrushProperty, value);
    }

    public static readonly DependencyProperty BorderBrushColorProperty =
        DependencyProperty.Register(
            nameof(BorderBrushColor), typeof(Brush), typeof(StatusBadge),
            new PropertyMetadata(null));

    public Brush? BorderBrushColor
    {
        get => (Brush?)GetValue(BorderBrushColorProperty);
        private set => SetValue(BorderBrushColorProperty, value);
    }

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge) badge.UpdateColors();
    }

    private void UpdateColors()
    {
        // Farben angelehnt an Theme-Brushes (Success/Warning/Danger/Info), aber mit
        // weichen Pastell-Hintergruenden fuer Badge-Optik. Border-Color = volle Akzent-Farbe.
        (Color bg, Color fg, Color border) = Variant switch
        {
            StatusBadgeVariant.Success => (Color.FromRgb(0xDC, 0xFC, 0xE7), Color.FromRgb(0x14, 0x53, 0x2D), Color.FromRgb(0x16, 0xA3, 0x4A)),
            StatusBadgeVariant.Warning => (Color.FromRgb(0xFE, 0xF3, 0xC7), Color.FromRgb(0x78, 0x35, 0x0F), Color.FromRgb(0xF5, 0x9E, 0x0B)),
            StatusBadgeVariant.Danger  => (Color.FromRgb(0xFE, 0xE2, 0xE2), Color.FromRgb(0x7F, 0x1D, 0x1D), Color.FromRgb(0xDC, 0x26, 0x26)),
            StatusBadgeVariant.Muted   => (Color.FromRgb(0xF1, 0xF5, 0xF9), Color.FromRgb(0x47, 0x55, 0x69), Color.FromRgb(0x94, 0xA3, 0xB8)),
            _                          => (Color.FromRgb(0xDB, 0xEA, 0xFE), Color.FromRgb(0x1E, 0x3A, 0x8A), Color.FromRgb(0x25, 0x63, 0xEB)),
        };

        BackgroundBrush  = new SolidColorBrush(bg);
        ForegroundBrush  = new SolidColorBrush(fg);
        BorderBrushColor = new SolidColorBrush(border);
    }
}

public enum StatusBadgeVariant
{
    Info,
    Success,
    Warning,
    Danger,
    Muted
}
