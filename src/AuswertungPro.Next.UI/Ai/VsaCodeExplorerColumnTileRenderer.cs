using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerColumnTileRenderResources(
    Brush AccentBrush,
    Brush TextBrush,
    Brush TextSecondaryBrush,
    Brush InvalidBrush,
    Color AccentColor,
    Func<string, Brush> GroupBrushResolver);

public static class VsaCodeExplorerColumnTileRenderer
{
    private static readonly FontFamily ConsolasFont = new("Consolas");

    public static Brush DefaultInvalidBrush { get; } = CreateFrozenBrush(0x9E, 0xAE, 0xC4);

    public static Button CreateButton(
        VsaCodeExplorerColumnTilePresentation presentation,
        TileItem tile,
        Style tileButtonStyle,
        VsaCodeExplorerColumnTileRenderResources resources,
        Action onClick)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(tileButtonStyle);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(onClick);

        var markerBrush = ResolveBrush(presentation.MarkerBrushRole, presentation.GroupColorHex, resources);
        var codeBrush = ResolveBrush(presentation.CodeBrushRole, presentation.GroupColorHex, resources);
        var descriptionBrush = ResolveBrush(presentation.DescriptionBrushRole, presentation.GroupColorHex, resources);

        var outerDock = new DockPanel { LastChildFill = true };
        var colorBar = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2, 0, 0, 2),
            Background = markerBrush,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0)
        };
        DockPanel.SetDock(colorBar, Dock.Left);
        outerDock.Children.Add(colorBar);

        var contentGrid = new Grid { Margin = new Thickness(8, 0, 4, 0) };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var codeText = new TextBlock
        {
            Text = presentation.LabelText,
            FontFamily = ConsolasFont,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = codeBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(codeText, 0);
        Grid.SetColumn(codeText, 0);
        contentGrid.Children.Add(codeText);

        var badgePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0)
        };
        foreach (var badge in presentation.Badges)
            badgePanel.Children.Add(CreateBadge(badge.Text, badge.ColorHex));

        Grid.SetRow(badgePanel, 0);
        Grid.SetColumn(badgePanel, 1);
        contentGrid.Children.Add(badgePanel);

        if (presentation.ShowDescription)
        {
            var descriptionText = new TextBlock
            {
                Text = presentation.DescriptionText,
                FontSize = 10,
                Foreground = descriptionBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(descriptionText, 1);
            Grid.SetColumn(descriptionText, 0);
            Grid.SetColumnSpan(descriptionText, 2);
            contentGrid.Children.Add(descriptionText);
        }

        outerDock.Children.Add(contentGrid);

        var button = new Button
        {
            Content = outerDock,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            MinHeight = 44,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 1, 2, 1),
            IsEnabled = true,
            Tag = tile,
            Style = tileButtonStyle
        };

        if (presentation.ShowSelectedChrome)
        {
            button.BorderThickness = new Thickness(2);
            button.BorderBrush = resources.AccentBrush;
            button.Background = new SolidColorBrush(resources.AccentColor) { Opacity = 0.12 };
        }

        if (presentation.ShowInvalidChrome)
        {
            button.Opacity = 0.7;
            codeText.TextDecorations = TextDecorations.Strikethrough;
            button.ToolTip = presentation.InvalidTooltip;
        }

        button.Click += (_, _) => onClick();
        return button;
    }

    public static Style CreateButtonStyle(Func<string, object> findResource)
    {
        ArgumentNullException.ThrowIfNull(findResource);

        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 36.0));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 0.0));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Medium));
        style.Setters.Add(new Setter(Control.BackgroundProperty, findResource("CardBrush")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, findResource("TextBrush")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, findResource("BorderBrush")));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(UIElement.SnapsToDevicePixelsProperty, true));

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        border.SetBinding(Border.BackgroundProperty, TemplateBinding("Background"));
        border.SetBinding(Border.BorderBrushProperty, TemplateBinding("BorderBrush"));
        border.SetBinding(Border.BorderThicknessProperty, TemplateBinding("BorderThickness"));
        border.SetBinding(Border.PaddingProperty, TemplateBinding("Padding"));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        border.AppendChild(presenter);

        template.VisualTree = border;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(
            Control.BackgroundProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F4FF")!),
            "bd"));
        template.Triggers.Add(hoverTrigger);

        var pressTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressTrigger.Setters.Add(new Setter(
            Control.BackgroundProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0EAFF")!),
            "bd"));
        template.Triggers.Add(pressTrigger);

        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.35));
        template.Triggers.Add(disabledTrigger);

        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Seal();
        return style;
    }

    private static Binding TemplateBinding(string propertyName)
        => new(propertyName)
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        };

    private static Brush ResolveBrush(
        VsaCodeExplorerColumnTileBrushRole role,
        string? groupColorHex,
        VsaCodeExplorerColumnTileRenderResources resources)
        => role switch
        {
            VsaCodeExplorerColumnTileBrushRole.Invalid => resources.InvalidBrush,
            VsaCodeExplorerColumnTileBrushRole.Accent => resources.AccentBrush,
            VsaCodeExplorerColumnTileBrushRole.Text => resources.TextBrush,
            VsaCodeExplorerColumnTileBrushRole.TextSecondary => resources.TextSecondaryBrush,
            _ => groupColorHex is not null ? resources.GroupBrushResolver(groupColorHex) : resources.AccentBrush
        };

    private static Border CreateBadge(string text, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        return new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(3, 0, 0, 0),
            Background = new SolidColorBrush(color) { Opacity = 0.12 },
            Child = new TextBlock
            {
                Text = text,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            }
        };
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
