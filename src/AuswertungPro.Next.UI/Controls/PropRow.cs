using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Controls;

public sealed class PropRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(PropRow), new PropertyMetadata(""));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}