using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class VsaCodeExplorerWindow : Window
{
    private readonly VsaCodeExplorerViewModel _vm;
    private readonly string? _videoPath;
    private readonly TimeSpan? _currentVideoTime;

    /// <summary>
    /// Optionaler Callback: Liefert einen Snapshot vom aktuellen VLC-Player-Frame.
    /// Wenn gesetzt, wird dieser statt ffmpeg fuer "Aus Video" verwendet.
    /// Gibt den Pfad zur gespeicherten PNG-Datei zurueck (oder null bei Fehler).
    /// </summary>
    public Func<string?>? LiveSnapshotProvider { get; set; }

    // Gecachte Brushes (aus Ressourcen, einmalig aufgeloest)
    private Brush? _accentBrush;
    private Brush? _successBrush;
    private Brush? _mutedBrush;
    private Brush? _textBrush;
    private Brush? _textSecondaryBrush;
    private Brush? _dangerBrush;
    private Color _colorAccent;
    private Color _colorSuccess;
    private Color _colorBorderLight;
    private Color _colorDanger;

    /// <summary>Ergebnis-Entry nach erfolgreichem Uebernehmen.</summary>
    public ProtocolEntry? SelectedEntry { get; private set; }

    /// <summary>Rohr-Kalibrierung (wird vom CodingModeWindow gesetzt und zurueckgelesen).</summary>
    public PipeCalibration? PipeCalibration { get; set; }

    public VsaCodeExplorerWindow(VsaCodeExplorerViewModel vm,
                                  string? videoPath = null,
                                  TimeSpan? currentVideoTime = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        _vm = vm;
        _videoPath = videoPath;
        _currentVideoTime = currentVideoTime;

        // Buttons
        BtnApply.Click += (_, _) => ApplyAndClose();
        BtnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        ResetButton.Click += (_, _) => _vm.ResetToMainCodes();

        // Foto 1 / Foto 2 Buttons
        BtnCaptureFoto1.Click += async (_, _) => await CapturePhotoAsync(0);
        BtnCaptureFoto2.Click += async (_, _) => await CapturePhotoAsync(1);

        // PhotoAssistant: Vermessen-Buttons + Doppelklick auf Thumbnails
        BtnMeasureFoto1.Click += (_, _) => OpenPhotoAssistant(0);
        BtnMeasureFoto2.Click += (_, _) => OpenPhotoAssistant(1);
        Foto1Image.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) OpenPhotoAssistant(0); };
        Foto2Image.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) OpenPhotoAssistant(1); };

        // Textbox-Bindings (Two-Way)
        TxtQ1Value.TextChanged += (_, _) => _vm.Q1Value = TxtQ1Value.Text;
        TxtQ2Value.TextChanged += (_, _) => _vm.Q2Value = TxtQ2Value.Text;
        TxtClockVon.TextChanged += (_, _) =>
        {
            _vm.ClockVon = TxtClockVon.Text;
            if (_vm.ClockMode == "single")
            {
                TxtClockBis.Text = string.IsNullOrWhiteSpace(TxtClockVon.Text) ? string.Empty : "00";
            }
            UpdateClockTransfer();
        };
        TxtClockBis.TextChanged += (_, _) =>
        {
            _vm.ClockBis = TxtClockBis.Text;
            UpdateClockTransfer();
        };
        TxtMeterStart.TextChanged += (_, _) => _vm.MeterStart = TxtMeterStart.Text;
        TxtMeterEnd.TextChanged += (_, _) => _vm.MeterEnd = TxtMeterEnd.Text;
        TxtZeit.TextChanged += (_, _) => _vm.Zeit = TxtZeit.Text;
        ChkStrecke.Checked += (_, _) =>
        {
            _vm.IsStreckenschaden = true;
            StreckeTypPanel.Visibility = Visibility.Visible;
            if (string.IsNullOrWhiteSpace(_vm.StreckenschadenTyp))
            {
                LstStreckeTyp.SelectedIndex = 0; // Default: Anfang
                _vm.StreckenschadenTyp = "Anfang";
            }
        };
        ChkStrecke.Unchecked += (_, _) =>
        {
            _vm.IsStreckenschaden = false;
            StreckeTypPanel.Visibility = Visibility.Collapsed;
            _vm.StreckenschadenTyp = "";
        };
        LstStreckeTyp.SelectionChanged += (_, _) =>
        {
            if (LstStreckeTyp.SelectedItem is ListBoxItem item)
                _vm.StreckenschadenTyp = item.Content?.ToString() ?? "";
        };

        // Rohrverbindung
        ChkRohrverbindung.Checked += (_, _) => _vm.AnRohrverbindung = true;
        ChkRohrverbindung.Unchecked += (_, _) => _vm.AnRohrverbindung = false;

        // Bemerkungen
        TxtBemerkungen.TextChanged += (_, _) => _vm.Bemerkungen = TxtBemerkungen.Text;

        // Clock Controls -> Textboxen (via DependencyPropertyDescriptor)
        var singleValueDesc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Controls.ClockPickerControl.ValueProperty, typeof(Controls.ClockPickerControl));
        singleValueDesc?.AddValueChanged(ClockSingle, (_, _) =>
        {
            var val = ClockSingle.Value;
            if (!string.IsNullOrWhiteSpace(val))
            {
                TxtClockVon.Text = val;
                TxtClockBis.Text = "00";
            }
        });

        var rangeFromDesc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Controls.ClockRangePickerControl.ValueFromProperty, typeof(Controls.ClockRangePickerControl));
        rangeFromDesc?.AddValueChanged(ClockRange, (_, _) =>
        {
            TxtClockVon.Text = ClockRange.ValueFrom ?? "";
        });

        var rangeToDesc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Controls.ClockRangePickerControl.ValueToProperty, typeof(Controls.ClockRangePickerControl));
        rangeToDesc?.AddValueChanged(ClockRange, (_, _) =>
        {
            TxtClockBis.Text = ClockRange.ValueTo ?? "";
        });

        // Uhr-Schnellwahl Buttons
        foreach (var btn in new[] { BtnClockScheitel, BtnClockSohle, BtnClockRechts, BtnClockGesamt, BtnClockKeine })
        {
            btn.Click += ClockPreset_Click;
        }

        // Keyboard
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnWindowClosed;

        // Initiale Werte setzen (leichtgewichtig)
        TxtMeterStart.Text = _vm.MeterStart;
        TxtMeterEnd.Text = _vm.MeterEnd;

        // Videozeit: ViewModel hat Vorrang, Fallback auf uebergebene Player-Zeit
        if (!string.IsNullOrWhiteSpace(_vm.Zeit))
        {
            TxtZeit.Text = _vm.Zeit;
        }
        else if (_currentVideoTime.HasValue && _currentVideoTime.Value > TimeSpan.Zero)
        {
            var t = _currentVideoTime.Value;
            var formatted = t.TotalHours >= 1
                ? t.ToString(@"hh\:mm\:ss")
                : t.ToString(@"mm\:ss");
            TxtZeit.Text = formatted;
            _vm.Zeit = formatted;
        }
        ChkStrecke.IsChecked = _vm.IsStreckenschaden;
        if (_vm.IsStreckenschaden)
        {
            StreckeTypPanel.Visibility = Visibility.Visible;
            if (_vm.StreckenschadenTyp == "Ende")
                LstStreckeTyp.SelectedIndex = 1;
            else
                LstStreckeTyp.SelectedIndex = 0;
        }
        ChkRohrverbindung.IsChecked = _vm.AnRohrverbindung;
        TxtBemerkungen.Text = _vm.Bemerkungen;
        TxtQ1Value.Text = _vm.Q1Value;
        TxtQ2Value.Text = _vm.Q2Value;
        TxtClockVon.Text = _vm.ClockVon;
        TxtClockBis.Text = _vm.ClockBis;

        // Schwere UI-Arbeit auf ContentRendered verschieben
        // → Fenster erscheint sofort, Tiles/Fotos werden danach gerendert
        ContentRendered += OnContentRendered;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;

        // Brushes einmalig cachen
        CacheBrushes();

        // VM-Events erst jetzt verbinden (verhindert fruehes Re-Rendering)
        _vm.PropertyChanged += Vm_PropertyChanged;

        // Fotos initialisieren
        UpdateFotoImages();

        // Multi-Column Collections binden (benannte Handler fuer Cleanup)
        _vm.GroupTiles.CollectionChanged += GroupTiles_Changed;
        _vm.CodeTiles.CollectionChanged += CodeTiles_Changed;
        _vm.Char1Tiles.CollectionChanged += Char1Tiles_Changed;
        _vm.Char2Tiles.CollectionChanged += Char2Tiles_Changed;

        // Initiale Multi-Column Befuellung
        _vm.PopulateAllColumns();

        // Initiale UI
        UpdateProgress();
        UpdateResultPanel();
        UpdateBreadcrumb();
        SyncValidationUi();
    }

    /// <summary>Ressourcen-Brushes einmalig aufloesen und cachen.</summary>
    private void CacheBrushes()
    {
        _accentBrush = (Brush)FindResource("AccentBrush");
        _successBrush = (Brush)FindResource("SuccessBrush");
        _mutedBrush = (Brush)FindResource("MutedBrush");
        _textBrush = (Brush)FindResource("TextBrush");
        _textSecondaryBrush = (Brush)FindResource("TextSecondaryBrush");
        _dangerBrush = (Brush)FindResource("DangerBrush");
        _colorAccent = (Color)FindResource("ColorAccent");
        _colorSuccess = (Color)FindResource("ColorSuccess");
        _colorBorderLight = (Color)FindResource("ColorBorderLight");
        _colorDanger = (Color)FindResource("ColorDanger");
    }

    // ═══════════════════════════════════════════════════════════════
    // VM → UI Synchronisation
    // ═══════════════════════════════════════════════════════════════

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(VsaCodeExplorerViewModel.CurrentLevel):
                    UpdateBreadcrumb();
                    UpdateProgress();
                    break;

                case nameof(VsaCodeExplorerViewModel.CurrentGroupColor):
                    UpdateProgress();
                    break;

                case nameof(VsaCodeExplorerViewModel.ShowResultPanel):
                    UpdateResultPanel();
                    UpdateProgress();
                    break;

                case nameof(VsaCodeExplorerViewModel.FinalCode):
                    TxtFinalCode.Text = _vm.FinalCode;
                    TxtCodePreview.Text = _vm.FinalCode;
                    break;

                case nameof(VsaCodeExplorerViewModel.FinalLabel):
                    TxtFinalLabel.Text = _vm.FinalLabel + (_vm.FinalSublabel is not null ? $" — {_vm.FinalSublabel}" : "");
                    break;

                case nameof(VsaCodeExplorerViewModel.FinalSublabel):
                    TxtFinalLabel.Text = _vm.FinalLabel + (_vm.FinalSublabel is not null ? $" — {_vm.FinalSublabel}" : "");
                    break;

                case nameof(VsaCodeExplorerViewModel.WarnMessage):
                    TxtWarn.Text = _vm.WarnMessage ?? "";
                    TxtWarn.Visibility = string.IsNullOrEmpty(_vm.WarnMessage) ? Visibility.Collapsed : Visibility.Visible;
                    break;

                case nameof(VsaCodeExplorerViewModel.Q1Rule):
                case nameof(VsaCodeExplorerViewModel.Q2Rule):
                    UpdateQuantPanel();
                    break;

                case nameof(VsaCodeExplorerViewModel.Q1Error):
                    TxtQ1Error.Text = _vm.Q1Error ?? "";
                    TxtQ1Error.Visibility = _vm.Q1Error is not null ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case nameof(VsaCodeExplorerViewModel.Q2Error):
                    TxtQ2Error.Text = _vm.Q2Error ?? "";
                    TxtQ2Error.Visibility = _vm.Q2Error is not null ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case nameof(VsaCodeExplorerViewModel.ClockMode):
                case nameof(VsaCodeExplorerViewModel.ClockHint):
                    UpdateClockPanel();
                    break;

                case nameof(VsaCodeExplorerViewModel.CanConfirm):
                    SyncValidationUi();
                    break;

                case nameof(VsaCodeExplorerViewModel.ValidationMessage):
                    SyncValidationUi();
                    break;
            }

            if (e.PropertyName is nameof(VsaCodeExplorerViewModel.BreadcrumbItems))
                UpdateBreadcrumb();
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // Multi-Column Tiles rendern (WinCan-Stil)
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    // Progress Bar
    // ═══════════════════════════════════════════════════════════════

    private void UpdateProgress()
    {
        var bars = new[] { ProgressBar0, ProgressBar1, ProgressBar2, ProgressBar3 };
        var labels = new[] { ProgressLabel0, ProgressLabel1, ProgressLabel2, ProgressLabel3 };
        var level = _vm.CurrentLevel;
        var isFinal = _vm.ShowResultPanel;

        var groupColor = _vm.CurrentGroupColor is not null
            ? GetGroupColorBrush(_vm.CurrentGroupColor).Color
            : _colorAccent;

        for (int i = 0; i < 4; i++)
        {
            Color barColor;
            if (isFinal && i >= level)
                barColor = _colorSuccess;
            else if (i < level)
                barColor = groupColor;
            else if (i == level)
                barColor = Color.FromArgb(0x80, groupColor.R, groupColor.G, groupColor.B);
            else
                barColor = _colorBorderLight;

            bars[i].Background = new SolidColorBrush(barColor);

            labels[i].FontWeight = i == level && !isFinal ? FontWeights.Bold : FontWeights.Normal;
            labels[i].Foreground = i <= level || isFinal
                ? _textSecondaryBrush
                : _mutedBrush;
        }

        TxtCodePreview.Text = _vm.FinalCode;
    }

    // ═══════════════════════════════════════════════════════════════
    // Result Panel
    // ═══════════════════════════════════════════════════════════════

    private void UpdateResultPanel()
    {
        if (_vm.ShowResultPanel)
        {
            ResultPanel.Visibility = Visibility.Visible;
            CodeHintPanel.Visibility = Visibility.Collapsed;

            TxtFinalCode.Text = _vm.FinalCode;
            TxtFinalLabel.Text = _vm.FinalLabel + (_vm.FinalSublabel is not null ? $" — {_vm.FinalSublabel}" : "");
            TxtWarn.Text = _vm.WarnMessage ?? "";
            TxtWarn.Visibility = string.IsNullOrEmpty(_vm.WarnMessage) ? Visibility.Collapsed : Visibility.Visible;

            UpdateQuantPanel();
            UpdateClockPanel();
        }
        else
        {
            ResultPanel.Visibility = Visibility.Collapsed;
            CodeHintPanel.Visibility = Visibility.Visible;
        }

        SyncValidationUi();
    }

    /// <summary>
    /// Initialisiert/aktualisiert den Footer-Validierungszustand robust,
    /// auch wenn CanConfirm bereits vor Event-Subscription gesetzt wurde.
    /// </summary>
    private void SyncValidationUi()
    {
        BtnApply.IsEnabled = _vm.CanConfirm;
        TxtValidation.Text = _vm.ValidationMessage ?? string.Empty;
        TxtValidation.Visibility = string.IsNullOrWhiteSpace(TxtValidation.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void UpdateQuantPanel()
    {
        var q1 = _vm.Q1Rule;
        var q2 = _vm.Q2Rule;

        if (q1 is null && q2 is null)
        {
            Q1Panel.Visibility = Visibility.Collapsed;
            Q2Panel.Visibility = Visibility.Collapsed;
            TxtNoQuant.Visibility = Visibility.Visible;
            return;
        }

        TxtNoQuant.Visibility = Visibility.Collapsed;

        if (q1 is not null)
        {
            Q1Panel.Visibility = Visibility.Visible;
            TxtQ1Label.Text = $"Q1: {q1.Label}";
            TxtQ1Unit.Text = q1.Einheit ?? "";

            var rangeText = "";
            if (q1.Min.HasValue && q1.Max.HasValue) rangeText = $"[{q1.Min}–{q1.Max}]";
            else if (q1.Min.HasValue) rangeText = $">= {q1.Min}";
            else if (q1.Max.HasValue) rangeText = $"<= {q1.Max}";
            if (q1.Hint is not null) rangeText += (rangeText.Length > 0 ? " " : "") + q1.Hint;
            TxtQ1Range.Text = rangeText;

            // Pflicht-Badge
            if (q1.Pflicht == "P")
            {
                BadgeQ1Pflicht.Visibility = Visibility.Visible;
                BadgeQ1Pflicht.Background = new SolidColorBrush(_colorDanger) { Opacity = 0.12 };
                ((TextBlock)BadgeQ1Pflicht.Child).Text = "PFLICHT";
                ((TextBlock)BadgeQ1Pflicht.Child).Foreground = _dangerBrush;
            }
            else
            {
                BadgeQ1Pflicht.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            Q1Panel.Visibility = Visibility.Collapsed;
        }

        if (q2 is not null)
        {
            Q2Panel.Visibility = Visibility.Visible;
            TxtQ2Label.Text = $"Q2: {q2.Label}";
            TxtQ2Unit.Text = q2.Einheit ?? "";
        }
        else
        {
            Q2Panel.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateClockPanel()
    {
        var mode = _vm.ClockMode;

        if (mode == "none")
        {
            ClockPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ClockPanel.Visibility = Visibility.Visible;

        TxtClockTitle.Text = mode == "single"
            ? "LAGE AM UMFANG (PUNKT)"
            : "LAGE AM UMFANG (VON–BIS)";

        if (_vm.ClockHint is not null)
        {
            TxtClockHint.Text = _vm.ClockHint;
            TxtClockHint.Visibility = Visibility.Visible;
        }
        else
        {
            TxtClockHint.Visibility = Visibility.Collapsed;
        }

        ClockSinglePanel.Visibility = mode == "single" ? Visibility.Visible : Visibility.Collapsed;
        ClockRangePanel.Visibility = mode == "range" ? Visibility.Visible : Visibility.Collapsed;

        // Bedienhinweis
        TxtClockUsageHint.Text = mode == "single"
            ? "Klick = Punkt (Mitte der Feststellung)"
            : "1. Klick = Von, 2. Klick = Bis (im Uhrzeigersinn)";

        // Schnellwahl: bei Single nur Punkt-Presets zeigen
        BtnClockRechts.Visibility = mode == "single" ? Visibility.Collapsed : Visibility.Visible;
        BtnClockGesamt.Visibility = mode == "single" ? Visibility.Collapsed : Visibility.Visible;

        if (mode == "single")
        {
            TxtClockBis.Text = string.IsNullOrWhiteSpace(TxtClockVon.Text) ? string.Empty : "00";

            ClockSingle.Value = string.Equals(TxtClockVon.Text?.Trim(), "00", StringComparison.Ordinal) ? "" : (TxtClockVon.Text ?? string.Empty);
        }
        else if (mode == "range")
        {
            ClockRange.ValueFrom = string.Equals(TxtClockVon.Text?.Trim(), "00", StringComparison.Ordinal) ? "" : (TxtClockVon.Text ?? string.Empty);
            ClockRange.ValueTo = string.Equals(TxtClockBis.Text?.Trim(), "00", StringComparison.Ordinal) ? "" : (TxtClockBis.Text ?? string.Empty);
        }

        UpdateClockTransfer();
    }

    // ═══════════════════════════════════════════════════════════════
    // Breadcrumb
    // ═══════════════════════════════════════════════════════════════

    private void UpdateBreadcrumb()
    {
        BreadcrumbPanel.Items.Clear();

        for (int i = 0; i < _vm.BreadcrumbItems.Count; i++)
        {
            var item = _vm.BreadcrumbItems[i];
            var isLast = i == _vm.BreadcrumbItems.Count - 1;

            if (i > 0)
            {
                BreadcrumbPanel.Items.Add(new TextBlock
                {
                    Text = "›",
                    FontSize = 10,
                    Foreground = _mutedBrush,
                    Margin = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var level = item.Level;
            var btn = new Button
            {
                Content = item.Label,
                Style = _toolbarButtonStyle ??= (Style)FindResource("ToolbarButton"),
                FontFamily = ConsolasFont,
                FontSize = 11,
                FontWeight = isLast ? FontWeights.SemiBold : FontWeights.Normal,
                Padding = new Thickness(3, 1, 3, 1),
                MinWidth = 0,
                MinHeight = 0,
                Foreground = isLast ? _textBrush : _mutedBrush,
            };

            if (!isLast)
                btn.Click += (_, _) => _vm.NavigateToBreadcrumb(level);

            BreadcrumbPanel.Items.Add(btn);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PhotoAssistant
    // ═══════════════════════════════════════════════════════════════

    /// <summary>PhotoAssistant oeffnen fuer Foto 1 oder 2.</summary>
    private void OpenPhotoAssistant(int photoIndex)
    {
        if (_vm.FotoPaths.Count <= photoIndex ||
            string.IsNullOrEmpty(_vm.FotoPaths[photoIndex]) ||
            !File.Exists(_vm.FotoPaths[photoIndex]))
        {
            MessageBox.Show(
                "Kein Foto vorhanden. Bitte zuerst ein Foto aufnehmen.",
                "PhotoAssistant", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var win = new PhotoMeasurementWindow(_vm.FotoPaths[photoIndex], PipeCalibration)
        {
            Owner = this
        };

        if (win.ShowDialog() == true && win.Result.Confirmed)
            ApplyPhotoResult(win.Result, photoIndex);
    }

    /// <summary>PhotoAssistant-Ergebnis uebernehmen.</summary>
    private void ApplyPhotoResult(Domain.Models.PhotoMeasurementResult result, int photoIndex)
    {
        // Q1-Wert uebernehmen
        if (result.Geometry?.FillPercent != null)
            TxtQ1Value.Text = result.Geometry.FillPercent.Value.ToString("F1");
        else if (result.Geometry?.Q1Mm != null)
            TxtQ1Value.Text = result.Geometry.Q1Mm.Value.ToString("F1");

        // Uhr-Position uebernehmen
        if (result.Geometry?.ClockFrom != null)
        {
            double clockHours = result.Geometry.ClockFrom.Value;
            int hours = (int)clockHours;
            int minutes = (int)((clockHours - hours) * 60);
            TxtClockVon.Text = $"{hours:D2}";
        }

        // Bogenwinkel uebernehmen
        if (result.Geometry?.ArcDegrees != null)
            TxtQ1Value.Text = result.Geometry.ArcDegrees.Value.ToString("F0");

        // Foto mit Overlay ersetzen
        if (!string.IsNullOrEmpty(result.OverlayPhotoPath) && File.Exists(result.OverlayPhotoPath))
        {
            while (_vm.FotoPaths.Count <= photoIndex)
                _vm.FotoPaths.Add("");
            _vm.FotoPaths[photoIndex] = result.OverlayPhotoPath;
            UpdateFotoImages();
        }

        // Kalibrierung zurueck uebernehmen
        if (result.UpdatedCalibration != null)
            PipeCalibration = result.UpdatedCalibration;
    }

    // ═══════════════════════════════════════════════════════════════
    // Foto
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Foto vom Video-Frame extrahieren und als Foto 1 oder 2 speichern.</summary>
    private async System.Threading.Tasks.Task CapturePhotoAsync(int fotoIndex)
    {
        BtnCaptureFoto1.IsEnabled = false;
        BtnCaptureFoto2.IsEnabled = false;
        try
        {
            string? tempPath = null;

            // Strategie 1: Live-Snapshot vom VLC-Player (aktuelles Bild)
            if (LiveSnapshotProvider != null)
            {
                tempPath = LiveSnapshotProvider();
            }

            // Strategie 2: Fallback auf ffmpeg (fuer den Fall dass kein Player verfuegbar)
            if (string.IsNullOrEmpty(tempPath) || !File.Exists(tempPath))
            {
                if (string.IsNullOrWhiteSpace(_videoPath) || !File.Exists(_videoPath))
                {
                    MessageBox.Show("Kein Video geladen.", "Foto", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var ffmpeg = FfmpegLocator.ResolveFfmpeg();
                var zeit = _currentVideoTime ?? TimeSpan.Zero;
                if (!string.IsNullOrWhiteSpace(TxtZeit.Text))
                {
                    var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss" };
                    if (TimeSpan.TryParseExact(TxtZeit.Text.Trim(), formats,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                        zeit = parsed;
                }

                var bytes = await VideoFrameExtractor.TryExtractFramePngAsync(
                    ffmpeg, _videoPath, zeit, CancellationToken.None);

                if (bytes is null || bytes.Length == 0)
                {
                    MessageBox.Show("Frame-Extraktion fehlgeschlagen.", "Foto", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                tempPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"vsa_foto{fotoIndex + 1}_{Guid.NewGuid():N}.png");
                await File.WriteAllBytesAsync(tempPath, bytes);
            }

            // Foto in FotoPaths an Index 0 oder 1 setzen
            while (_vm.FotoPaths.Count <= fotoIndex)
                _vm.FotoPaths.Add("");
            _vm.FotoPaths[fotoIndex] = tempPath;

            UpdateFotoImages();
        }
        finally
        {
            BtnCaptureFoto1.IsEnabled = true;
            BtnCaptureFoto2.IsEnabled = true;
        }
    }

    /// <summary>Foto 1/2 Vorschau-Images aktualisieren.</summary>
    private void UpdateFotoImages()
    {
        // Foto 1
        if (_vm.FotoPaths.Count > 0 && !string.IsNullOrEmpty(_vm.FotoPaths[0]) && File.Exists(_vm.FotoPaths[0]))
        {
            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(_vm.FotoPaths[0]);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = 180;
                bi.EndInit();
                Foto1Image.Source = bi;
                Foto1Placeholder.Visibility = Visibility.Collapsed;
            }
            catch { Foto1Placeholder.Visibility = Visibility.Visible; }
        }
        else
        {
            Foto1Image.Source = null;
            Foto1Placeholder.Visibility = Visibility.Visible;
        }

        // Foto 2
        if (_vm.FotoPaths.Count > 1 && !string.IsNullOrEmpty(_vm.FotoPaths[1]) && File.Exists(_vm.FotoPaths[1]))
        {
            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(_vm.FotoPaths[1]);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = 180;
                bi.EndInit();
                Foto2Image.Source = bi;
                Foto2Placeholder.Visibility = Visibility.Collapsed;
            }
            catch { Foto2Placeholder.Visibility = Visibility.Visible; }
        }
        else
        {
            Foto2Image.Source = null;
            Foto2Placeholder.Visibility = Visibility.Visible;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Uhr-Schnellwahl
    // ═══════════════════════════════════════════════════════════════

    private void ClockPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag) return;
        var parts = tag.Split(',');
        if (parts.Length != 2) return;
        TxtClockVon.Text = parts[0];
        TxtClockBis.Text = parts[1];
    }

    /// <summary>Transfer-Anzeige neben Von/Bis aktualisieren.</summary>
    private void UpdateClockTransfer()
    {
        var von = string.IsNullOrWhiteSpace(TxtClockVon.Text) ? "--" : TxtClockVon.Text.Trim().PadLeft(2, '0');
        var bis = string.IsNullOrWhiteSpace(TxtClockBis.Text) ? "--" : TxtClockBis.Text.Trim().PadLeft(2, '0');
        TxtClockTransfer.Text = $"Transfer: {von} {bis}";
    }

    // ═══════════════════════════════════════════════════════════════
    // Apply / Close
    // ═══════════════════════════════════════════════════════════════

    private void ApplyAndClose()
    {
        if (!_vm.CanConfirm) return;

        SelectedEntry = _vm.BuildProtocolEntry();
        DialogResult = true;
        Close();
    }

    // ═══════════════════════════════════════════════════════════════
    // Keyboard
    // ═══════════════════════════════════════════════════════════════

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_vm.ShowResultPanel || _vm.CurrentLevel > 0)
            {
                e.Handled = true;
                _vm.NavigateBack();
                return;
            }
        }

        if (e.Key == Key.Back && Keyboard.FocusedElement is not TextBox)
        {
            e.Handled = true;
            _vm.NavigateBack();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            ApplyAndClose();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _vm.PropertyChanged -= Vm_PropertyChanged;
        _vm.GroupTiles.CollectionChanged -= GroupTiles_Changed;
        _vm.CodeTiles.CollectionChanged -= CodeTiles_Changed;
        _vm.Char1Tiles.CollectionChanged -= Char1Tiles_Changed;
        _vm.Char2Tiles.CollectionChanged -= Char2Tiles_Changed;
        PreviewKeyDown -= OnPreviewKeyDown;
    }

    // Benannte CollectionChanged Handler (fuer Cleanup via -=)
    private void GroupTiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => RenderColumnTiles(GroupList, _vm.GroupTiles, tile => _vm.SelectGroup(tile.Key)));
    private void CodeTiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => RenderColumnTiles(CodeList, _vm.CodeTiles, tile => _vm.SelectCode(tile.Key)));
    private void Char1Tiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => RenderColumnTiles(Char1List, _vm.Char1Tiles, tile => _vm.SelectChar1(tile.Key)));
    private void Char2Tiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => RenderColumnTiles(Char2List, _vm.Char2Tiles, tile => _vm.SelectChar2(tile.Key)));
}
