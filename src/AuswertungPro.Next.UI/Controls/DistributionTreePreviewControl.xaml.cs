using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Grafische Ordnerbaum-Vorschau einer Verteil-Variante: eingerueckte Zeilen mit
/// Ordner-/PDF-/Video-Icon. Datenquelle sind <see cref="DistributionTreeNode"/>-Knoten.
/// </summary>
public partial class DistributionTreePreviewControl : UserControl
{
    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes),
        typeof(IReadOnlyList<DistributionTreeNode>),
        typeof(DistributionTreePreviewControl),
        new PropertyMetadata(null));

    public IReadOnlyList<DistributionTreeNode>? Nodes
    {
        get => (IReadOnlyList<DistributionTreeNode>?)GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public DistributionTreePreviewControl()
    {
        InitializeComponent();
    }
}

/// <summary>Einrueckung eines Baum-Knotens: Tiefe * 18 px Links-Margin.</summary>
public sealed class DistributionTreeDepthToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var depth = value is int d ? d : 0;
        return new Thickness(depth * 18, 1, 0, 1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
