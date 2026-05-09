using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

using AuswertungPro.Next.Application.CodeCatalog;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// VsaCodeExplorerWindow Tile-Rendering: Erzeugt Buttons fuer Gruppe/Code/
// Char1/Char2-Spalten, baut Style + Badges, setzt Frozen-Brushes pro
// Gruppen-Farbe. Aus dem Hauptdatei extrahiert (Slice 27a).
public partial class VsaCodeExplorerWindow
{
    private void RenderColumnTiles(ItemsControl list, System.Collections.ObjectModel.ObservableCollection<TileItem> tiles, Action<TileItem> onSelect)
    {
        list.Items.Clear();
        foreach (var tile in tiles)
        {
            var btn = CreateColumnTileButton(tile, onSelect);
            list.Items.Add(btn);
        }

        // Char2-Spalte + Trennlinie ausblenden wenn leer
        var hasChar2 = _vm.Char2Tiles.Count > 0;
        Char2Column.Width = hasChar2 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Char2Sep.Width = hasChar2 ? GridLength.Auto : new GridLength(0);
        Char2SepBorder.Visibility = hasChar2 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Kompakter Button fuer die Multi-Column Ansicht.</summary>
    private Button CreateColumnTileButton(TileItem tile, Action<TileItem> onSelect)
    {
        _actionCardButtonStyle ??= (Style)FindResource("ActionCardButton");

        var groupBrush = tile.GroupColor is not null
            ? (Brush)GetGroupColorBrush(tile.GroupColor)
            : _accentBrush!;

        // Aeusserer Container: farbige Markierung links + Inhalt
        var outerDock = new DockPanel { LastChildFill = true };

        // Farbige Gruppenmarkierung links (4px breit, volle Hoehe)
        var colorBar = new Border
        {
            Width = 4,
            CornerRadius = new CornerRadius(2, 0, 0, 2),
            Background = tile.IsInvalid ? InvalidBrush
                : tile.IsSelected ? _accentBrush
                : groupBrush,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0)
        };
        DockPanel.SetDock(colorBar, Dock.Left);
        outerDock.Children.Add(colorBar);

        // Inhalt: Grid mit Code-Zeile (feste Hoehe) + Beschreibung
        var contentGrid = new Grid { Margin = new Thickness(8, 0, 4, 0) };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Code-Label (links, fest ausgerichtet)
        var codeTb = new TextBlock
        {
            Text = tile.Label,
            FontFamily = ConsolasFont,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = tile.IsInvalid ? InvalidBrush
                : tile.IsSelected ? _accentBrush
                : groupBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(codeTb, 0);
        Grid.SetColumn(codeTb, 0);
        contentGrid.Children.Add(codeTb);

        // Badges (rechts ausgerichtet, gleiche Zeile)
        var badgePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(6, 0, 0, 0)
        };
        if (tile.BadgeText is not null)
            badgePanel.Children.Add(CreateBadge(tile.BadgeText, tile.BadgeColor ?? "#2563EB"));
        if (tile.IsFinal && !tile.IsSelected)
            badgePanel.Children.Add(CreateBadge("End", "#16A34A"));
        Grid.SetRow(badgePanel, 0);
        Grid.SetColumn(badgePanel, 1);
        contentGrid.Children.Add(badgePanel);

        // Beschreibung (zweite Zeile, volle Breite)
        if (!string.IsNullOrEmpty(tile.Description))
        {
            var descTb = new TextBlock
            {
                Text = tile.Description,
                FontSize = 10,
                Foreground = tile.IsInvalid ? InvalidBrush
                    : tile.IsSelected ? _textBrush
                    : _textSecondaryBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            Grid.SetRow(descTb, 1);
            Grid.SetColumn(descTb, 0);
            Grid.SetColumnSpan(descTb, 2);
            contentGrid.Children.Add(descTb);
        }

        outerDock.Children.Add(contentGrid);

        // Eigener Style: ActionCardButton hat HorizontalAlignment=Center im Template
        // und Padding=16,8 — beides bricht das Alignment der Farbbalken.
        // Deshalb: Style uebernehmen, aber Padding auf 0 (Kontrolle via contentGrid.Margin).
        _tileButtonStyle ??= BuildTileButtonStyle();

        var btn = new Button
        {
            Content = outerDock,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            MinHeight = 44,
            Padding = new Thickness(0),
            Margin = new Thickness(2, 1, 2, 1),
            IsEnabled = true,
            Tag = tile,
            Style = _tileButtonStyle
        };

        // Hervorhebung fuer ausgewaehltes Element (kraeftiger Rahmen + Hintergrund)
        if (tile.IsSelected)
        {
            btn.BorderThickness = new Thickness(2);
            btn.BorderBrush = _accentBrush;
            btn.Background = new SolidColorBrush(_colorAccent) { Opacity = 0.12 };
        }

        if (tile.IsInvalid)
        {
            btn.Opacity = 0.7;
            codeTb.TextDecorations = TextDecorations.Strikethrough;
            btn.ToolTip = "Als ungueltig markiert - Auswahl ist trotzdem erlaubt.";
        }

        btn.Click += (_, _) => onSelect(tile);
        return btn;
    }

    // Legacy: wird nicht mehr verwendet aber fuer Kompatibilitaet beibehalten
    private void RenderTiles()
    {
    }

    // Gecachte Styles und Consolas-Font
    private Style? _actionCardButtonStyle;
    private Style? _toolbarButtonStyle;
    private Style? _tileButtonStyle;
    private static readonly FontFamily ConsolasFont = new("Consolas");
    private static readonly SolidColorBrush InvalidBrush = CreateFrozenBrush(0x9E, 0xAE, 0xC4);
    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Eigener Button-Style fuer Tile-Karten: Padding=0 (wird intern gesteuert),
    /// ContentPresenter auf Stretch (nicht Center) damit Farbbalken korrekt aligned.
    /// </summary>
    private Style BuildTileButtonStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(MinHeightProperty, 36.0));
        style.Setters.Add(new Setter(MinWidthProperty, 0.0));
        style.Setters.Add(new Setter(FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(FontWeightProperty, FontWeights.Medium));
        style.Setters.Add(new Setter(BackgroundProperty, FindResource("CardBrush")));
        style.Setters.Add(new Setter(ForegroundProperty, FindResource("TextBrush")));
        style.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(1)));
        style.Setters.Add(new Setter(BorderBrushProperty, FindResource("BorderBrush")));
        style.Setters.Add(new Setter(CursorProperty, Cursors.Hand));
        style.Setters.Add(new Setter(SnapsToDevicePixelsProperty, true));

        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border), "bd");
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(SnapsToDevicePixelsProperty, true);
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Stretch);
        border.AppendChild(presenter);

        template.VisualTree = border;

        // Hover-Trigger
        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F4FF")!), "bd"));
        template.Triggers.Add(hoverTrigger);

        // Pressed-Trigger
        var pressTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        pressTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0EAFF")!), "bd"));
        template.Triggers.Add(pressTrigger);

        // Disabled-Trigger
        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.35));
        template.Triggers.Add(disabledTrigger);

        style.Setters.Add(new Setter(TemplateProperty, template));
        style.Seal();
        return style;
    }

    // Cache fuer GroupColor-Brushes (vermeidet wiederholtes ColorConverter.ConvertFromString)
    private readonly System.Collections.Generic.Dictionary<string, SolidColorBrush> _groupColorCache = new();

    private SolidColorBrush GetGroupColorBrush(string colorHex)
    {
        if (!_groupColorCache.TryGetValue(colorHex, out var brush))
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            brush.Freeze();
            _groupColorCache[colorHex] = brush;
        }
        return brush;
    }

    private Button CreateTileButton(TileItem tile)
    {
        _actionCardButtonStyle ??= (Style)FindResource("ActionCardButton");

        // Grid-Layout: Spalten [Code | Beschreibung | Badges]
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });   // Code
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Beschreibung
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // Badges/Pfeil

        var groupBrush = tile.GroupColor is not null
            ? (Brush)GetGroupColorBrush(tile.GroupColor)
            : _accentBrush!;

        // ── Code-Label (Spalte 0, feste Breite) ──
        var codeTb = new TextBlock
        {
            Text = tile.Label,
            FontFamily = ConsolasFont,
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = tile.IsInvalid ? InvalidBrush
                : tile.IsFinal ? _successBrush
                : groupBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(codeTb, 0);
        grid.Children.Add(codeTb);

        // ── Beschreibung (Spalte 1, fuellt Rest) ──
        var descTb = new TextBlock
        {
            Text = tile.Description ?? "",
            FontSize = 12,
            Foreground = tile.IsInvalid ? InvalidBrush : _textBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 4, 0)
        };
        Grid.SetColumn(descTb, 1);
        grid.Children.Add(descTb);

        // ── Rechte Seite: Badges + Pfeil (Spalte 2) ──
        var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(rightPanel, 2);

        if (tile.BadgeText is not null)
            rightPanel.Children.Add(CreateBadge(tile.BadgeText, tile.BadgeColor ?? "#2563EB"));
        if (tile.IsFinal)
            rightPanel.Children.Add(CreateBadge("End", "#16A34A"));
        if (tile.IsSteuer)
            rightPanel.Children.Add(CreateBadge("Stc", "#2563EB"));
        if (tile.IsInvalid)
            rightPanel.Children.Add(CreateBadge("ungueltig", "#DC2626"));

        if (!tile.IsFinal && !tile.IsInvalid)
        {
            rightPanel.Children.Add(new TextBlock
            {
                Text = "\u2192",
                FontSize = 15,
                Foreground = _mutedBrush,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        grid.Children.Add(rightPanel);

        // ── Button mit farbiger Linie links (alle Ebenen) ──
        var btn = new Button
        {
            Content = grid,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 4),
            IsEnabled = true,
            Tag = tile,
            Style = _actionCardButtonStyle
        };

        if (tile.GroupColor is not null)
        {
            btn.BorderThickness = new Thickness(3, 1, 1, 1);
            btn.BorderBrush = GetGroupColorBrush(tile.GroupColor);
        }

        if (tile.IsInvalid)
        {
            btn.Opacity = 0.7;
            codeTb.TextDecorations = TextDecorations.Strikethrough;
            btn.ToolTip = "Als ungueltig markiert - Auswahl ist trotzdem erlaubt.";
        }

        btn.Click += (_, _) => _vm.SelectTile(tile);
        return btn;
    }

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

}
