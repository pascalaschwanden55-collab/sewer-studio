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
            var result = VsaCodeExplorerClockTextWorkflow.ApplyVonChanged(
                TxtClockVon.Text,
                TxtClockBis.Text,
                _vm.ClockMode);

            _vm.ClockVon = result.ClockVon;
            VsaCodeExplorerClockTextRenderer.ApplyVonChanged(
                result,
                new VsaCodeExplorerClockTextRenderTargets(TxtClockBis, TxtClockTransfer));
        };
        TxtClockBis.TextChanged += (_, _) =>
        {
            var result = VsaCodeExplorerClockTextWorkflow.ApplyBisChanged(
                TxtClockVon.Text,
                TxtClockBis.Text);

            _vm.ClockBis = result.ClockBis;
            VsaCodeExplorerClockTextRenderer.ApplyBisChanged(
                result,
                new VsaCodeExplorerClockTextRenderTargets(TxtClockBis, TxtClockTransfer));
        };
        TxtMeterStart.TextChanged += (_, _) => _vm.MeterStart = TxtMeterStart.Text;
        TxtMeterEnd.TextChanged += (_, _) => _vm.MeterEnd = TxtMeterEnd.Text;
        TxtZeit.TextChanged += (_, _) => _vm.Zeit = TxtZeit.Text;
        ChkStrecke.Checked += (_, _) =>
            ApplyStreckenschadenChange(
                VsaCodeExplorerStreckenschadenWorkflow.ApplyChecked(_vm.StreckenschadenTyp));
        ChkStrecke.Unchecked += (_, _) =>
            ApplyStreckenschadenChange(
                VsaCodeExplorerStreckenschadenWorkflow.ApplyUnchecked());
        LstStreckeTyp.SelectionChanged += (_, _) =>
        {
            if (LstStreckeTyp.SelectedItem is ListBoxItem item)
                _vm.StreckenschadenTyp =
                    VsaCodeExplorerStreckenschadenWorkflow.ApplySelectionChanged(item.Content?.ToString());
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
            VsaCodeExplorerClockPickerRenderer.ApplySingleValueChanged(
                ClockSingle.Value,
                new VsaCodeExplorerClockPickerRenderTargets(TxtClockVon, TxtClockBis));
        });

        var rangeFromDesc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Controls.ClockRangePickerControl.ValueFromProperty, typeof(Controls.ClockRangePickerControl));
        rangeFromDesc?.AddValueChanged(ClockRange, (_, _) =>
        {
            VsaCodeExplorerClockPickerRenderer.ApplyRangeFromChanged(
                ClockRange.ValueFrom,
                new VsaCodeExplorerClockPickerRenderTargets(TxtClockVon, TxtClockBis));
        });

        var rangeToDesc = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
            Controls.ClockRangePickerControl.ValueToProperty, typeof(Controls.ClockRangePickerControl));
        rangeToDesc?.AddValueChanged(ClockRange, (_, _) =>
        {
            VsaCodeExplorerClockPickerRenderer.ApplyRangeToChanged(
                ClockRange.ValueTo,
                new VsaCodeExplorerClockPickerRenderTargets(TxtClockVon, TxtClockBis));
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
        VsaCodeExplorerInitialFieldsRenderer.Apply(
            new VsaCodeExplorerInitialFieldValues(
                _vm.MeterStart,
                _vm.MeterEnd,
                _vm.Bemerkungen,
                _vm.Q1Value,
                _vm.Q2Value,
                _vm.ClockVon,
                _vm.ClockBis),
            new VsaCodeExplorerInitialFieldsRenderTargets(
                TxtMeterStart,
                TxtMeterEnd,
                TxtBemerkungen,
                TxtQ1Value,
                TxtQ2Value,
                TxtClockVon,
                TxtClockBis));

        var initialTime = VsaCodeExplorerInitialTimeWorkflow.Build(_vm.Zeit, _currentVideoTime);
        if (initialTime.TextBoxText is { } textBoxText)
            TxtZeit.Text = textBoxText;
        if (initialTime.ViewModelZeit is { } viewModelZeit)
            _vm.Zeit = viewModelZeit;
        var initialStreckenschaden = VsaCodeExplorerStreckenschadenWorkflow.BuildInitial(
            _vm.IsStreckenschaden,
            _vm.StreckenschadenTyp);
        ChkStrecke.IsChecked = initialStreckenschaden.IsStreckenschaden;
        ApplyStreckenschadenChange(initialStreckenschaden);
        ChkRohrverbindung.IsChecked = _vm.AnRohrverbindung;

        // Schwere UI-Arbeit auf ContentRendered verschieben
        // → Fenster erscheint sofort, Tiles/Fotos werden danach gerendert
        ContentRendered += OnContentRendered;
    }

    private void ApplyStreckenschadenChange(VsaCodeExplorerStreckenschadenChange change)
    {
        _vm.IsStreckenschaden = change.IsStreckenschaden;
        _vm.StreckenschadenTyp = change.StreckenschadenTyp;
        VsaCodeExplorerStreckenschadenRenderer.Apply(
            change.Presentation,
            new VsaCodeExplorerStreckenschadenRenderTargets(StreckeTypPanel, LstStreckeTyp));
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
        var propertyName = e.PropertyName;
        VsaCodeExplorerDispatchWorkflow.DispatchPropertyChanged(
            Dispatcher.CheckAccess(),
            () => ApplyViewModelPropertyChanged(propertyName),
            action => _ = Dispatcher.BeginInvoke(action));
    }

    private void ApplyViewModelPropertyChanged(string? propertyName)
    {
        var actions = VsaCodeExplorerPropertyChangeWorkflow.Resolve(propertyName);

        if (actions.UpdateBreadcrumb)
            UpdateBreadcrumb();

        if (actions.UpdateResultPanel)
            UpdateResultPanel();

        if (actions.UpdateProgress)
            UpdateProgress();

        if (actions.UpdateQuantPanel)
            UpdateQuantPanel();

        if (actions.UpdateQ1Error)
        {
            var presentation = VsaCodeExplorerFieldErrorPresenter.Build(_vm.Q1Error);
            VsaCodeExplorerFieldErrorRenderer.Apply(presentation, TxtQ1Error);
        }

        if (actions.UpdateQ2Error)
        {
            var presentation = VsaCodeExplorerFieldErrorPresenter.Build(_vm.Q2Error);
            VsaCodeExplorerFieldErrorRenderer.Apply(presentation, TxtQ2Error);
        }

        if (actions.UpdateClockPanel)
            UpdateClockPanel();

        if (actions.SyncValidation)
            SyncValidationUi();
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

        var columnLayout = VsaCodeExplorerColumnLayoutPresenter.Build(_vm.Char2Tiles.Count);
        Char2Column.Width = columnLayout.ShowChar2Column ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Char2Sep.Width = columnLayout.ShowChar2Column ? GridLength.Auto : new GridLength(0);
        Char2SepBorder.Visibility = columnLayout.ShowChar2Column ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Kompakter Button fuer die Multi-Column Ansicht.</summary>
    private Button CreateColumnTileButton(TileItem tile, Action<TileItem> onSelect)
    {
        var presentation = VsaCodeExplorerColumnTilePresenter.Build(tile);
        _tileButtonStyle ??= VsaCodeExplorerColumnTileRenderer.CreateButtonStyle(FindResource);
        return VsaCodeExplorerColumnTileRenderer.CreateButton(
            presentation,
            tile,
            _tileButtonStyle,
            new VsaCodeExplorerColumnTileRenderResources(
                _accentBrush ?? Brushes.DodgerBlue,
                _textBrush ?? Brushes.Black,
                _textSecondaryBrush ?? Brushes.Gray,
                VsaCodeExplorerColumnTileRenderer.DefaultInvalidBrush,
                _colorAccent,
                GetGroupColorBrush),
            () => onSelect(tile));

        // und Padding=16,8 — beides bricht das Alignment der Farbbalken.
    }

    // Gecachte Styles und Consolas-Font
    private Style? _toolbarButtonStyle;
    private Style? _tileButtonStyle;
    private static readonly FontFamily ConsolasFont = new("Consolas");

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

    // ═══════════════════════════════════════════════════════════════
    // Progress Bar
    // ═══════════════════════════════════════════════════════════════

    private void UpdateProgress()
    {
        var presentation = VsaCodeExplorerProgressPresenter.Build(
            _vm.CurrentLevel,
            _vm.ShowResultPanel,
            _vm.FinalCode);
        var groupColor = _vm.CurrentGroupColor is not null
            ? GetGroupColorBrush(_vm.CurrentGroupColor).Color
            : _colorAccent;

        VsaCodeExplorerProgressRenderer.Apply(
            presentation,
            new VsaCodeExplorerProgressRenderTargets(
                [ProgressBar0, ProgressBar1, ProgressBar2, ProgressBar3],
                [ProgressLabel0, ProgressLabel1, ProgressLabel2, ProgressLabel3],
                TxtCodePreview),
            new VsaCodeExplorerProgressRenderBrushes(
                _colorSuccess,
                groupColor,
                _colorBorderLight,
                _textSecondaryBrush ?? Brushes.Gray,
                _mutedBrush ?? Brushes.Gray));
    }

    // ═══════════════════════════════════════════════════════════════
    // Result Panel
    // ═══════════════════════════════════════════════════════════════

    private void UpdateResultPanel()
    {
        var presentation = VsaCodeExplorerResultPanelPresenter.Build(
            _vm.ShowResultPanel,
            _vm.FinalCode,
            _vm.FinalLabel,
            _vm.FinalSublabel,
            _vm.WarnMessage);

        VsaCodeExplorerResultPanelRenderer.Apply(
            presentation,
            new VsaCodeExplorerResultPanelRenderTargets(
                ResultPanel,
                CodeHintPanel,
                TxtFinalCode,
                TxtFinalLabel,
                TxtWarn));

        if (presentation.ShouldUpdateDetailPanels)
        {
            UpdateQuantPanel();
            UpdateClockPanel();
        }

        SyncValidationUi();
    }

    /// <summary>
    /// Initialisiert/aktualisiert den Footer-Validierungszustand robust,
    /// auch wenn CanConfirm bereits vor Event-Subscription gesetzt wurde.
    /// </summary>
    private void SyncValidationUi()
    {
        var presentation = VsaCodeExplorerValidationPresenter.Build(_vm.CanConfirm, _vm.ValidationMessage);
        VsaCodeExplorerValidationRenderer.Apply(
            presentation,
            new VsaCodeExplorerValidationRenderTargets(BtnApply, TxtValidation));
    }

    private void UpdateQuantPanel()
    {
        var presentation = VsaCodeExplorerQuantPanelPresenter.Build(_vm.Q1Rule, _vm.Q2Rule);
        VsaCodeExplorerQuantPanelRenderer.Apply(
            presentation,
            new VsaCodeExplorerQuantPanelRenderTargets(
                TxtNoQuant,
                Q1Panel,
                TxtQ1Label,
                TxtQ1Unit,
                TxtQ1Range,
                BadgeQ1Pflicht,
                Q2Panel,
                TxtQ2Label,
                TxtQ2Unit),
            new VsaCodeExplorerQuantPanelRenderBrushes(
                _colorDanger,
                _dangerBrush ?? Brushes.Red));
    }

    private void UpdateClockPanel()
    {
        var presentation = VsaCodeExplorerClockPanelPresenter.Build(
            _vm.ClockMode,
            _vm.ClockHint,
            TxtClockVon.Text,
            TxtClockBis.Text);
        VsaCodeExplorerClockPanelRenderer.Apply(
            presentation,
            new VsaCodeExplorerClockPanelRenderTargets(
                ClockPanel,
                TxtClockTitle,
                TxtClockHint,
                ClockSinglePanel,
                ClockRangePanel,
                TxtClockUsageHint,
                BtnClockRechts,
                BtnClockGesamt,
                TxtClockBis,
                value => ClockSingle.Value = value,
                value => ClockRange.ValueFrom = value,
                value => ClockRange.ValueTo = value,
                TxtClockTransfer));
    }

    // ═══════════════════════════════════════════════════════════════
    // Breadcrumb
    // ═══════════════════════════════════════════════════════════════

    private void UpdateBreadcrumb()
    {
        var presentation = VsaCodeExplorerBreadcrumbPresenter.Build(_vm.BreadcrumbItems);
        VsaCodeExplorerBreadcrumbRenderer.Apply(
            presentation,
            new VsaCodeExplorerBreadcrumbRenderTargets(
                BreadcrumbPanel,
                _toolbarButtonStyle ??= (Style)FindResource("ToolbarButton"),
                new VsaCodeExplorerBreadcrumbRenderBrushes(
                    _textBrush ?? Brushes.Black,
                    _mutedBrush ?? Brushes.Gray),
                ConsolasFont,
                _vm.NavigateToBreadcrumb));
    }

    // ═══════════════════════════════════════════════════════════════
    // PhotoAssistant
    // ═══════════════════════════════════════════════════════════════

    /// <summary>PhotoAssistant oeffnen fuer Foto 1 oder 2.</summary>
    private void OpenPhotoAssistant(int photoIndex)
    {
        var decision = VsaCodeExplorerPhotoAssistantOpenPolicy.Resolve(_vm.FotoPaths, photoIndex);
        if (!decision.CanOpen || decision.PhotoPath is null)
        {
            DialogHost.Current.Info(
                decision.Message,
                decision.Title);
            return;
        }

        var win = new PhotoMeasurementWindow(decision.PhotoPath, PipeCalibration)
        {
            Owner = this
        };

        if (win.ShowDialog() == true && win.Result.Confirmed)
            ApplyPhotoResult(win.Result, photoIndex);
    }

    /// <summary>PhotoAssistant-Ergebnis uebernehmen.</summary>
    private void ApplyPhotoResult(Domain.Models.PhotoMeasurementResult result, int photoIndex)
    {
        var applied = VsaCodeExplorerPhotoResultWorkflow.Apply(
            result,
            photoIndex,
            _vm.FotoPaths);

        VsaCodeExplorerPhotoResultRenderer.Apply(
            applied,
            new VsaCodeExplorerPhotoResultRenderTargets(TxtQ1Value, TxtClockVon));

        if (applied.PhotoPathChanged)
            UpdateFotoImages();

        if (applied.UpdatedCalibration is not null)
            PipeCalibration = applied.UpdatedCalibration;
    }

    // ═══════════════════════════════════════════════════════════════
    // Foto
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Foto vom Video-Frame extrahieren und als Foto 1 oder 2 speichern.</summary>
    private async System.Threading.Tasks.Task CapturePhotoAsync(int fotoIndex)
    {
        VsaCodeExplorerPhotoCaptureButtonsRenderer.Apply(
            isCaptureRunning: true,
            new VsaCodeExplorerPhotoCaptureButtonsRenderTargets(BtnCaptureFoto1, BtnCaptureFoto2));
        try
        {
            var result = await VsaCodeExplorerPhotoCaptureWorkflow.CaptureWithDefaultsAsync(
                fotoIndex,
                _vm.FotoPaths,
                LiveSnapshotProvider,
                _videoPath,
                _currentVideoTime,
                TxtZeit.Text,
                CancellationToken.None);

            if (result.Outcome == VsaCodeExplorerPhotoCaptureOutcome.MissingVideo)
            {
                DialogHost.Current.Info(result.Message, result.Title);
                return;
            }

            if (result.Outcome == VsaCodeExplorerPhotoCaptureOutcome.ExtractionFailed)
            {
                DialogHost.Current.Warn(result.Message, result.Title);
                return;
            }

            UpdateFotoImages();
        }
        finally
        {
            VsaCodeExplorerPhotoCaptureButtonsRenderer.Apply(
                isCaptureRunning: false,
                new VsaCodeExplorerPhotoCaptureButtonsRenderTargets(BtnCaptureFoto1, BtnCaptureFoto2));
        }
    }

    /// <summary>Foto 1/2 Vorschau-Images aktualisieren.</summary>
    private void UpdateFotoImages()
    {
        var preview = VsaCodeExplorerPhotoPreviewPlanner.Plan(_vm.FotoPaths);
        VsaCodeExplorerPhotoPreviewRenderer.Apply(
            preview,
            new VsaCodeExplorerPhotoPreviewRenderTargets(
                Foto1Image,
                Foto1Placeholder,
                Foto2Image,
                Foto2Placeholder,
                VsaCodeExplorerPhotoPreviewRenderer.LoadBitmapImage));
    }

    // ═══════════════════════════════════════════════════════════════
    // Uhr-Schnellwahl
    // ═══════════════════════════════════════════════════════════════

    private void ClockPreset_Click(object sender, RoutedEventArgs e)
    {
        var tag = sender is System.Windows.Controls.Button btn ? btn.Tag as string : null;
        var result = VsaCodeExplorerClockPresetWorkflow.Resolve(tag);
        VsaCodeExplorerClockPresetRenderer.Apply(
            result,
            new VsaCodeExplorerClockPresetRenderTargets(TxtClockVon, TxtClockBis));
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
        var action = VsaCodeExplorerKeyboardNavigationPolicy.Resolve(
            e.Key,
            Keyboard.Modifiers,
            Keyboard.FocusedElement is TextBox,
            _vm.ShowResultPanel,
            _vm.CurrentLevel);

        if (action is null)
            return;

        e.Handled = true;

        if (action == VsaCodeExplorerKeyboardNavigationAction.NavigateBack)
        {
            _vm.NavigateBack();
            return;
        }

        if (action == VsaCodeExplorerKeyboardNavigationAction.ApplyAndClose)
            ApplyAndClose();
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
        => VsaCodeExplorerDispatchWorkflow.ScheduleColumnRender(
            () => RenderColumnTiles(GroupList, _vm.GroupTiles, tile => _vm.SelectGroup(tile.Key)),
            action => _ = Dispatcher.BeginInvoke(action));

    private void CodeTiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => VsaCodeExplorerDispatchWorkflow.ScheduleColumnRender(
            () => RenderColumnTiles(CodeList, _vm.CodeTiles, tile => _vm.SelectCode(tile.Key)),
            action => _ = Dispatcher.BeginInvoke(action));

    private void Char1Tiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => VsaCodeExplorerDispatchWorkflow.ScheduleColumnRender(
            () => RenderColumnTiles(Char1List, _vm.Char1Tiles, tile => _vm.SelectChar1(tile.Key)),
            action => _ = Dispatcher.BeginInvoke(action));

    private void Char2Tiles_Changed(object? s, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => VsaCodeExplorerDispatchWorkflow.ScheduleColumnRender(
            () => RenderColumnTiles(Char2List, _vm.Char2Tiles, tile => _vm.SelectChar2(tile.Key)),
            action => _ = Dispatcher.BeginInvoke(action));
}
