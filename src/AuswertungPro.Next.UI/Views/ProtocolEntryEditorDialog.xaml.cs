using System.Globalization;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views;

public partial class ProtocolEntryEditorDialog : Window
{
    private readonly ServiceProvider? _sp;
    private readonly ProtocolEntryVM _entryVm;
    private readonly ProtocolEntryEditorViewModel? _paramVm;
    private readonly ProtocolEntryEditorKiViewModel? _kiVm;
    private readonly string? _haltungId;
    private readonly string? _videoPath;
    private readonly string? _projectFolder;
    private CancellationTokenSource? _kiSuggestionCts;
    private bool _isKiBusy;
    private bool _isNormalizingCode;

    public ProtocolEntryEditorDialog()
        : this(new ProtocolEntryVM(new ProtocolEntry()), null, null, null, null)
    {
    }

    public ProtocolEntryEditorDialog(
        ProtocolEntryVM entryVm,
        ServiceProvider? sp = null,
        string? haltungId = null,
        string? videoPath = null,
        string? projectFolder = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _entryVm = entryVm;
        _sp = sp;
        _haltungId = haltungId;
        _videoPath = videoPath;
        _projectFolder = projectFolder;

        _paramVm = _sp?.CodeCatalog is null ? null : new ProtocolEntryEditorViewModel(_sp.CodeCatalog);
        _kiVm = _paramVm is null || _sp is null
            ? null
            : new ProtocolEntryEditorKiViewModel(_paramVm, _sp.ProtocolAi, _entryVm);

        LoadFromEntry();

        CatalogButton.Click += (_, _) => OpenCodePicker();
        KiSuggestButton.Click += async (_, _) => await SuggestWithKiAsync();
        ZeitFromVideoButton.Click += (_, _) => SetZeitFromVideo();
        SeekToZeitButton.Click += (_, _) => SeekToZeit();
        OkButton.Click += (_, _) => ApplyAndClose();
        CatalogButton.ToolTip = "Shortcut: Ctrl+K";
        KiSuggestButton.ToolTip = "Shortcut: Ctrl+L";
        ZeitFromVideoButton.ToolTip = "Shortcut: F6";
        SeekToZeitButton.ToolTip = "Shortcut: F7";
        OkButton.ToolTip = "Shortcut: Ctrl+S";
        CodeTextBox.TextChanged += (_, _) => OnCodeTextChanged();
        MeterStartTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        MeterEndTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        ZeitTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        MpegTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        StreckenschadenCheckBox.Checked += (_, _) => ApplyLiveValidation();
        StreckenschadenCheckBox.Unchecked += (_, _) => ApplyLiveValidation();
        MeterStartTextBox.LostFocus += (_, _) => NormalizeNumberText(MeterStartTextBox);
        MeterEndTextBox.LostFocus += (_, _) => NormalizeNumberText(MeterEndTextBox);
        ZeitTextBox.LostFocus += (_, _) => NormalizeTimeText(ZeitTextBox);
        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        HookVsaValidationEvents();
        PreviewKeyDown += OnDialogPreviewKeyDown;
        _entryVm.PropertyChanged += EntryVm_PropertyChanged;
        Closed += OnDialogClosed;

        HookParameterValidationEvents();
        ApplyLiveValidation();

        if (string.IsNullOrWhiteSpace(_entryVm.Code))
        {
            Dispatcher.BeginInvoke(new Action(OpenCodePicker), DispatcherPriority.Loaded);
        }
    }

    private void LoadFromEntry()
    {
        CodeTextBox.Text = _entryVm.Code;
        MeterStartTextBox.Text = _entryVm.MeterStart?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        MeterEndTextBox.Text = _entryVm.MeterEnd?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        ZeitTextBox.Text = _entryVm.Zeit is null ? string.Empty : FormatTime(_entryVm.Zeit.Value);
        MpegTextBox.Text = _entryVm.Mpeg ?? string.Empty;
        BeschreibungTextBox.Text = _entryVm.Beschreibung;
        StreckenschadenCheckBox.IsChecked = _entryVm.Model.IsStreckenschaden;
        AiStatusText.Text = string.IsNullOrWhiteSpace(_entryVm.AiSuggestedCode)
            ? string.Empty
            : $"Vorheriger KI-Vorschlag: {_entryVm.AiSuggestedCode} ({_entryVm.AiConfidence:P0})";
        ValidationStatus.Text = string.Empty;

        if (_paramVm is not null)
        {
            _paramVm.LoadFromEntry(_entryVm.Model);
            ParametersItems.ItemsSource = _paramVm.Parameters;
            HookParameterValidationEvents();
        }

        ApplyLiveValidation();
    }

    private void RefreshParameterBindings()
    {
        if (_paramVm is null)
            return;

        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        if (string.Equals(_paramVm.Code, code, StringComparison.OrdinalIgnoreCase))
            return;

        _paramVm.Code = code;
        ParametersItems.ItemsSource = _paramVm.Parameters;
        HookParameterValidationEvents();
        ApplyLiveValidation();
    }

    private void OnCodeTextChanged()
    {
        if (_isNormalizingCode)
            return;

        var raw = CodeTextBox.Text ?? string.Empty;
        var normalized = NormalizeCode(raw);
        if (!string.Equals(raw, normalized, StringComparison.Ordinal))
        {
            var caret = CodeTextBox.CaretIndex;
            _isNormalizingCode = true;
            CodeTextBox.Text = normalized;
            CodeTextBox.CaretIndex = Math.Min(caret, normalized.Length);
            _isNormalizingCode = false;
        }

        RefreshParameterBindings();
        ApplyLiveValidation();
    }

    // Delegation an ProtocolEntryInputNormalizer (Application-Schicht)
    private static string NormalizeCode(string value)
        => ProtocolEntryInputNormalizer.NormalizeCode(value);

    private void HookParameterValidationEvents()
    {
        if (_paramVm is null)
            return;

        foreach (var parameter in _paramVm.Parameters)
        {
            parameter.PropertyChanged -= Parameter_PropertyChanged;
            parameter.PropertyChanged += Parameter_PropertyChanged;
        }
    }

    private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CodeParameterViewModel.Value) or nameof(CodeParameterViewModel.IsValid))
            ApplyLiveValidation();
    }

    private void HookVsaValidationEvents()
    {
        VsaDistanzTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaVideoTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaQ1TextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaQ2TextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaStreckeTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaAnsichtTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaSchachtbereichTextBox.TextChanged += (_, _) => ApplyLiveValidation();
        VsaAnmerkungTextBox.TextChanged += (_, _) => ApplyLiveValidation();

        VsaUhrVonComboBox.SelectionChanged += (_, _) => ApplyLiveValidation();
        VsaUhrBisComboBox.SelectionChanged += (_, _) => ApplyLiveValidation();
        VsaEzComboBox.SelectionChanged += (_, _) => ApplyLiveValidation();

        VsaVerbindungCheckBox.Checked += (_, _) => ApplyLiveValidation();
        VsaVerbindungCheckBox.Unchecked += (_, _) => ApplyLiveValidation();

        VsaDistanzTextBox.LostFocus += (_, _) => NormalizeNumberText(VsaDistanzTextBox);
        VsaVideoTextBox.LostFocus += (_, _) => NormalizeTimeText(VsaVideoTextBox);
        VsaQ1TextBox.LostFocus += (_, _) => NormalizeNumberText(VsaQ1TextBox);
        VsaQ2TextBox.LostFocus += (_, _) => NormalizeNumberText(VsaQ2TextBox);
        VsaStreckeTextBox.LostFocus += (_, _) => NormalizeStreckeText(VsaStreckeTextBox);
        VsaUhrVonComboBox.LostFocus += (_, _) => NormalizeClockCombo(VsaUhrVonComboBox);
        VsaUhrBisComboBox.LostFocus += (_, _) => NormalizeClockCombo(VsaUhrBisComboBox);
        VsaEzComboBox.LostFocus += (_, _) => NormalizeEzCombo(VsaEzComboBox);
        VsaSchachtbereichTextBox.LostFocus += (_, _) => NormalizeSchachtbereichText(VsaSchachtbereichTextBox);
    }

    private void EntryVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProtocolEntryVM.VsaUhrVon) or nameof(ProtocolEntryVM.VsaUhrBis)
            or nameof(ProtocolEntryVM.VsaDistanz) or nameof(ProtocolEntryVM.VsaVideo)
            or nameof(ProtocolEntryVM.VsaQ1) or nameof(ProtocolEntryVM.VsaQ2)
            or nameof(ProtocolEntryVM.VsaStrecke) or nameof(ProtocolEntryVM.VsaEz)
            or nameof(ProtocolEntryVM.VsaSchachtbereich))
        {
            ApplyLiveValidation();
        }
    }

    private void OnDialogPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            ApplyAndClose();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            e.Handled = true;
            OpenCodePicker();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L)
        {
            e.Handled = true;
            _ = SuggestWithKiAsync();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F6)
        {
            e.Handled = true;
            SetZeitFromVideo();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F7)
        {
            e.Handled = true;
            SeekToZeit();
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None || e.Key != Key.Enter)
            return;

        if (Keyboard.FocusedElement is TextBox { AcceptsReturn: true })
            return;

        e.Handled = true;
        var request = new TraversalRequest(FocusNavigationDirection.Next);
        (Keyboard.FocusedElement as UIElement)?.MoveFocus(request);
    }

    private void ApplyLiveValidation()
    {
        var errors = new List<string>();

        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        var hasCatalog = _sp?.CodeCatalog is not null;
        var codeOk = !string.IsNullOrWhiteSpace(code);
        var codeInCatalog = false;
        if (!codeOk)
            errors.Add("Code ist erforderlich.");
        else if (!hasCatalog || !_sp!.CodeCatalog.TryGet(code, out _))
        {
            codeOk = false;
            errors.Add("Code ist nicht im Katalog vorhanden.");
        }
        else
        {
            codeInCatalog = true;
        }

        var meterStartOk = TryParseOptionalDouble(MeterStartTextBox.Text, out var meterStart);
        var meterEndOk = TryParseOptionalDouble(MeterEndTextBox.Text, out var meterEnd);
        if (!meterStartOk)
            errors.Add("MeterStart ist ungueltig.");
        if (!meterEndOk)
            errors.Add("MeterEnd ist ungueltig.");

        var zeitOk = TryParseOptionalTimeSpan(ZeitTextBox.Text, out _);
        if (!zeitOk)
            errors.Add("Zeit ist ungueltig.");

        var streckeOk = true;
        if (StreckenschadenCheckBox.IsChecked == true)
        {
            if (!meterStart.HasValue || !meterEnd.HasValue)
            {
                streckeOk = false;
                errors.Add("Streckenschaden: MeterStart und MeterEnde sind Pflicht.");
            }
            else if (meterEnd < meterStart)
            {
                streckeOk = false;
                errors.Add("Streckenschaden: MeterEnde muss groesser/gleich MeterStart sein.");
            }
        }

        var vsaErrors = ValidateVsaUiFields(
            code,
            codeInCatalog,
            out var vsaDistanzOk,
            out var vsaVideoOk,
            out var vsaUhrVonOk,
            out var vsaUhrBisOk,
            out var vsaQ1Ok,
            out var vsaQ2Ok,
            out var vsaStreckeOk,
            out var vsaEzOk,
            out var vsaSchachtbereichOk);
        errors.AddRange(vsaErrors);

        if (_paramVm is not null)
        {
            if (!string.Equals(_paramVm.Code, code, StringComparison.OrdinalIgnoreCase))
                _paramVm.Code = code;

            _paramVm.Validate();
            if (!_paramVm.IsValid)
                errors.AddRange(_paramVm.ValidationMessages);
        }

        SetControlValidationState(CodeTextBox, codeOk, "Code fehlt oder nicht im Katalog.");
        SetControlValidationState(MeterStartTextBox, meterStartOk, "Numerischer Wert erwartet.");
        SetControlValidationState(MeterEndTextBox, meterEndOk, "Numerischer Wert erwartet.");
        SetControlValidationState(ZeitTextBox, zeitOk, "Erlaubt: mm:ss oder hh:mm:ss.");
        SetControlValidationState(StreckenschadenCheckBox, streckeOk, "Streckenschaden benötigt gueltige Meter von/bis.");
        SetControlValidationState(VsaDistanzTextBox, vsaDistanzOk, "Numerischer Wert erwartet.");
        SetControlValidationState(VsaVideoTextBox, vsaVideoOk, "Erlaubt: mm:ss oder hh:mm:ss.");
        SetControlValidationState(VsaUhrVonComboBox, vsaUhrVonOk, "Erlaubt: 00 bis 12.");
        SetControlValidationState(VsaUhrBisComboBox, vsaUhrBisOk, "Erlaubt: 00 bis 12.");
        SetControlValidationState(VsaQ1TextBox, vsaQ1Ok, "Numerischer Wert erwartet.");
        SetControlValidationState(VsaQ2TextBox, vsaQ2Ok, "Numerischer Wert erwartet.");
        SetControlValidationState(VsaStreckeTextBox, vsaStreckeOk, "Erlaubt: A1/B1/C1...");
        SetControlValidationState(VsaEzComboBox, vsaEzOk, "Erlaubt: EZ0 bis EZ4.");
        SetControlValidationState(VsaSchachtbereichTextBox, vsaSchachtbereichOk, "Erlaubt: A/B/D/F/H/I/J.");

        var uniqueErrors = errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueErrors.Count == 0)
            ValidationStatus.Text = "Eingabe gültig.";
        else
            ValidationStatus.Text = string.Join(Environment.NewLine, uniqueErrors.Take(12));

        OkButton.IsEnabled = uniqueErrors.Count == 0 && !_isKiBusy;
    }

    private static void SetControlValidationState(Control control, bool isValid, string? tooltip = null)
    {
        if (isValid)
        {
            control.ClearValue(Control.BorderBrushProperty);
            control.ClearValue(Control.BorderThicknessProperty);
            control.ClearValue(ToolTipProperty);
            return;
        }

        control.BorderBrush = Brushes.OrangeRed;
        control.BorderThickness = new Thickness(2);
        if (!string.IsNullOrWhiteSpace(tooltip))
            control.ToolTip = tooltip;
    }

    private void NormalizeNumberText(TextBox textBox, bool revalidate = true)
    {
        if (!TryParseOptionalDouble(textBox.Text, out var value))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        var normalized = value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;
        if (!string.Equals(textBox.Text, normalized, StringComparison.Ordinal))
            textBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    private void NormalizeTimeText(TextBox textBox, bool revalidate = true)
    {
        if (!TryParseOptionalTimeSpan(textBox.Text, out var value))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        var normalized = value.HasValue ? FormatTime(value.Value) : string.Empty;
        if (!string.Equals(textBox.Text, normalized, StringComparison.Ordinal))
            textBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    private void NormalizeStreckeText(TextBox textBox, bool revalidate = true)
    {
        if (!TryNormalizeStrecke(textBox.Text, out var normalized, out _))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        if (!string.Equals(textBox.Text, normalized, StringComparison.Ordinal))
            textBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    private void NormalizeClockCombo(ComboBox comboBox, bool revalidate = true)
    {
        if (!TryNormalizeClockPosition(comboBox.Text, out var normalized, out _))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        if (!string.Equals(comboBox.Text, normalized, StringComparison.Ordinal))
            comboBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    private void NormalizeEzCombo(ComboBox comboBox, bool revalidate = true)
    {
        if (!TryNormalizeEz(comboBox.Text, out var normalized, out _))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        if (!string.Equals(comboBox.Text, normalized, StringComparison.Ordinal))
            comboBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    private void NormalizeSchachtbereichText(TextBox textBox, bool revalidate = true)
    {
        if (!TryNormalizeSchachtbereich(textBox.Text, out var normalized, out _))
        {
            if (revalidate)
                ApplyLiveValidation();
            return;
        }

        if (!string.Equals(textBox.Text, normalized, StringComparison.Ordinal))
            textBox.Text = normalized;

        if (revalidate)
            ApplyLiveValidation();
    }

    // Delegation an ProtocolEntryValidator (Application-Schicht)
    private List<string> ValidateVsaUiFields(
        string code,
        bool codeInCatalog,
        out bool vsaDistanzOk,
        out bool vsaVideoOk,
        out bool vsaUhrVonOk,
        out bool vsaUhrBisOk,
        out bool vsaQ1Ok,
        out bool vsaQ2Ok,
        out bool vsaStreckeOk,
        out bool vsaEzOk,
        out bool vsaSchachtbereichOk)
    {
        CodeDefinition? catalogDef = null;
        if (codeInCatalog && _sp?.CodeCatalog is not null)
            _sp.CodeCatalog.TryGet(code, out catalogDef);

        var inputs = new VsaFieldInputs
        {
            Distanz = VsaDistanzTextBox.Text,
            Video = VsaVideoTextBox.Text,
            UhrVon = VsaUhrVonComboBox.Text,
            UhrBis = VsaUhrBisComboBox.Text,
            Q1 = VsaQ1TextBox.Text,
            Q2 = VsaQ2TextBox.Text,
            Strecke = VsaStreckeTextBox.Text,
            Ez = VsaEzComboBox.Text,
            Schachtbereich = VsaSchachtbereichTextBox.Text,
            IsStreckenschaden = StreckenschadenCheckBox.IsChecked == true
        };

        var result = ProtocolEntryValidator.ValidateVsaFields(code, inputs, catalogDef, requireDistanz: true);
        vsaDistanzOk = result.DistanzOk;
        vsaVideoOk = result.VideoOk;
        vsaUhrVonOk = result.UhrVonOk;
        vsaUhrBisOk = result.UhrBisOk;
        vsaQ1Ok = result.Q1Ok;
        vsaQ2Ok = result.Q2Ok;
        vsaStreckeOk = result.StreckeOk;
        vsaEzOk = result.EzOk;
        vsaSchachtbereichOk = result.SchachtbereichOk;
        return result.Errors.ToList();
    }

    private void NormalizeAllStrictInputs()
    {
        NormalizeNumberText(MeterStartTextBox, revalidate: false);
        NormalizeNumberText(MeterEndTextBox, revalidate: false);
        NormalizeTimeText(ZeitTextBox, revalidate: false);

        NormalizeNumberText(VsaDistanzTextBox, revalidate: false);
        NormalizeTimeText(VsaVideoTextBox, revalidate: false);
        NormalizeNumberText(VsaQ1TextBox, revalidate: false);
        NormalizeNumberText(VsaQ2TextBox, revalidate: false);
        NormalizeStreckeText(VsaStreckeTextBox, revalidate: false);
        NormalizeClockCombo(VsaUhrVonComboBox, revalidate: false);
        NormalizeClockCombo(VsaUhrBisComboBox, revalidate: false);
        NormalizeEzCombo(VsaEzComboBox, revalidate: false);
        NormalizeSchachtbereichText(VsaSchachtbereichTextBox, revalidate: false);
    }

    private async Task SuggestWithKiAsync()
    {
        if (_isKiBusy)
            return;

        if (_kiVm is null)
        {
            ValidationStatus.Text = "KI nicht verfügbar: Service fehlt.";
            return;
        }

        var request = new ProtocolEntryKiSuggestionRequest(
            ProjectFolderAbs: ResolveProjectFolder(),
            HaltungId: _haltungId,
            MeterStartText: MeterStartTextBox.Text ?? string.Empty,
            MeterEndText: MeterEndTextBox.Text ?? string.Empty,
            ZeitText: ZeitTextBox.Text ?? string.Empty,
            ExistingCode: CodeTextBox.Text ?? string.Empty,
            ExistingText: BeschreibungTextBox.Text ?? string.Empty,
            VideoPathAbs: ResolveExistingPath(_videoPath),
            ImagePathsAbs: ResolveImagePaths(_entryVm.Model.FotoPaths));

        _isKiBusy = true;
        KiSuggestButton.IsEnabled = false;
        AiStatusText.Text = "KI-Vorschlag wird geladen...";
        ValidationStatus.Text = string.Empty;

        var cancellation = new CancellationTokenSource();
        _kiSuggestionCts = cancellation;
        ProtocolEntryKiSuggestionResult? result = null;
        using var _aiToken = Services.AiActivityTracker.Begin("KI-Codevorschlag");
        try
        {
            result = await _kiVm.SuggestAsync(request, cancellation.Token);
        }
        finally
        {
            if (ReferenceEquals(_kiSuggestionCts, cancellation))
                _kiSuggestionCts = null;
            cancellation.Dispose();
            _isKiBusy = false;

            if (IsLoaded)
            {
                KiSuggestButton.IsEnabled = true;
                ApplyLiveValidation();
                ApplyKiSuggestionResult(result);
            }
        }
    }

    private void ApplyKiSuggestionResult(ProtocolEntryKiSuggestionResult? result)
    {
        if (result is null)
            return;

        if (!string.IsNullOrWhiteSpace(result.StatusText))
            AiStatusText.Text = result.StatusText;

        if (!string.IsNullOrWhiteSpace(result.AcceptedCode))
            CodeTextBox.Text = result.AcceptedCode;

        if (!string.IsNullOrWhiteSpace(result.ValidationText))
            ValidationStatus.Text = result.ValidationText;
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        _entryVm.PropertyChanged -= EntryVm_PropertyChanged;
        _kiSuggestionCts?.Cancel();
    }

    private void OpenCodePicker()
    {
        if (_sp?.CodeCatalog is null)
        {
            ValidationStatus.Text = "Code-Katalog ist nicht verfügbar.";
            return;
        }

        var vm = new ProtocolCodePickerViewModel(_sp.CodeCatalog, _entryVm);
        var picker = new ProtocolCodePickerDialog
        {
            Owner = this,
            DataContext = vm
        };

        if (picker.ShowDialog() == true)
        {
            LoadFromEntry();
            ValidationStatus.Text = "Code übernommen.";
        }
    }

    private void ApplyAndClose()
    {
        ValidationStatus.Text = string.Empty;
        NormalizeAllStrictInputs();
        ApplyLiveValidation();

        if (!TryParseOptionalDouble(MeterStartTextBox.Text, out var meterStart))
        {
            ValidationStatus.Text = "MeterStart ist ungültig.";
            return;
        }

        if (!TryParseOptionalDouble(MeterEndTextBox.Text, out var meterEnd))
        {
            ValidationStatus.Text = "MeterEnd ist ungültig.";
            return;
        }

        if (!TryParseOptionalTimeSpan(ZeitTextBox.Text, out var zeit))
        {
            ValidationStatus.Text = "Zeit ist ungültig.";
            return;
        }

        var mpeg = (MpegTextBox.Text ?? string.Empty).Trim();
        if (zeit is null && !string.IsNullOrWhiteSpace(mpeg))
            zeit = TryParseTimeFallback(mpeg);

        var code = NormalizeCode(CodeTextBox.Text ?? string.Empty);
        if (!string.Equals(CodeTextBox.Text, code, StringComparison.Ordinal))
            CodeTextBox.Text = code;

        if (string.IsNullOrWhiteSpace(code))
        {
            ValidationStatus.Text = "Code ist erforderlich.";
            ApplyLiveValidation();
            return;
        }

        if (_sp?.CodeCatalog is null || !_sp.CodeCatalog.TryGet(code, out _))
        {
            ValidationStatus.Text = "Code ist nicht im Katalog vorhanden.";
            ApplyLiveValidation();
            return;
        }

        if (_paramVm is not null)
        {
            _paramVm.Code = code;
            _paramVm.Validate();
            if (!_paramVm.IsValid)
            {
                ValidationStatus.Text = "Pflicht-Parameter fehlen oder sind ungültig.";
                ApplyLiveValidation();
                return;
            }
        }

        _entryVm.Code = code;
        _entryVm.Beschreibung = BeschreibungTextBox.Text ?? string.Empty;
        _entryVm.MeterStart = meterStart;
        _entryVm.MeterEnd = meterEnd;
        _entryVm.Zeit = zeit;
        _entryVm.Mpeg = string.IsNullOrWhiteSpace(mpeg) ? null : mpeg;
        _entryVm.Model.IsStreckenschaden = StreckenschadenCheckBox.IsChecked == true;

        if (_entryVm.Model.IsStreckenschaden)
        {
            if (!_entryVm.MeterStart.HasValue || !_entryVm.MeterEnd.HasValue)
            {
                ValidationStatus.Text = "Streckenschaden: MeterStart und MeterEnde sind Pflicht.";
                return;
            }
            if (_entryVm.MeterEnd < _entryVm.MeterStart)
            {
                ValidationStatus.Text = "Streckenschaden: MeterEnde muss größer/gleich MeterStart sein.";
                return;
            }
        }

        if (_entryVm.Model.CodeMeta is null && _entryVm.Code.Length > 0)
        {
            _entryVm.Model.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = _entryVm.Code,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        if (_entryVm.Model.CodeMeta is not null)
        {
            _entryVm.Model.CodeMeta.Code = _entryVm.Code;
            _entryVm.Model.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (_paramVm is not null && _entryVm.Code.Length > 0)
            _paramVm.ApplyToEntry(_entryVm.Model);

        _entryVm.EnsureVsaDefaults();
        _entryVm.ApplyStreckenLogik();

        var vsaErrors = ValidateVsaFields();
        if (vsaErrors.Count > 0)
        {
            ValidationStatus.Text = string.Join(Environment.NewLine, vsaErrors);
            return;
        }

        if (string.IsNullOrWhiteSpace(_entryVm.Beschreibung)
            && _sp?.CodeCatalog is not null
            && _sp.CodeCatalog.TryGet(_entryVm.Code, out var def))
        {
            _entryVm.Beschreibung = BuildDefaultDescription(def, _entryVm.Parameters, _entryVm.MeterStart, _entryVm.MeterEnd);
        }

        if (_entryVm.Model.Ai is not null)
        {
            var suggested = _entryVm.Model.Ai.SuggestedCode?.Trim();
            _entryVm.Model.Ai.FinalCode = _entryVm.Code;
            _entryVm.Model.Ai.Accepted = !string.IsNullOrWhiteSpace(suggested)
                                          && string.Equals(suggested, _entryVm.Code, StringComparison.OrdinalIgnoreCase);
        }

        DialogResult = true;
        Close();
    }

    // Delegation an ProtocolEntryValidator (Application-Schicht)
    private List<string> ValidateVsaFields()
    {
        var code = (_entryVm.Code ?? string.Empty).Trim();
        if (code.Length == 0)
            return new List<string>();

        CodeDefinition? catalogDef = null;
        _sp?.CodeCatalog?.TryGet(code, out catalogDef);

        var inputs = new VsaFieldInputs
        {
            Distanz = _entryVm.VsaDistanz,
            Video = _entryVm.VsaVideo,
            UhrVon = _entryVm.VsaUhrVon,
            UhrBis = _entryVm.VsaUhrBis,
            Q1 = _entryVm.VsaQ1,
            Q2 = _entryVm.VsaQ2,
            Strecke = _entryVm.VsaStrecke,
            Ez = _entryVm.VsaEz,
            Schachtbereich = _entryVm.VsaSchachtbereich,
            IsStreckenschaden = _entryVm.Model.IsStreckenschaden
        };

        return ProtocolEntryValidator.ValidateVsaFields(code, inputs, catalogDef, requireDistanz: true).Errors.ToList();
    }

    private string ResolveProjectFolder()
    {
        if (!string.IsNullOrWhiteSpace(_projectFolder))
            return _projectFolder;

        var fromSettings = _sp?.Settings.LastProjectPath;
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            // Projekt-ROOT (nicht GetDirectoryName): projekt.json kann unter Projektdateien\ liegen.
            var dir = AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(fromSettings)
                      ?? Path.GetDirectoryName(fromSettings);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    private string? ResolveExistingPath(string? raw)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return path;

        if (Path.IsPathRooted(path))
            return null;

        var baseDir = ResolveProjectFolder();
        if (string.IsNullOrWhiteSpace(baseDir))
            return null;

        var combined = Path.GetFullPath(Path.Combine(baseDir, path));
        return File.Exists(combined) ? combined : null;
    }

    private IReadOnlyList<string> ResolveImagePaths(IReadOnlyList<string> rawPaths)
    {
        var result = new List<string>();
        foreach (var raw in rawPaths)
        {
            var path = ResolveExistingPath(raw);
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }

        return result;
    }

    // Delegation an ProtocolEntryInputNormalizer (Application-Schicht)
    private static bool TryParseOptionalDouble(string raw, out double? value)
        => ProtocolEntryInputNormalizer.TryParseOptionalDouble(raw, out value);

    private static bool TryNormalizeClockPosition(string? raw, out string normalized, out bool hasValue)
        => ProtocolEntryInputNormalizer.TryNormalizeClockPosition(raw, out normalized, out hasValue);

    private static bool TryNormalizeStrecke(string? raw, out string normalized, out bool hasValue)
        => ProtocolEntryInputNormalizer.TryNormalizeStrecke(raw, out normalized, out hasValue);

    private static bool TryNormalizeEz(string? raw, out string normalized, out bool hasValue)
        => ProtocolEntryInputNormalizer.TryNormalizeEz(raw, out normalized, out hasValue);

    private static bool TryNormalizeSchachtbereich(string? raw, out string normalized, out bool hasValue)
        => ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(raw, out normalized, out hasValue);

    private static bool TryParseOptionalTimeSpan(string raw, out TimeSpan? value)
        => ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(raw, out value);

    private static TimeSpan? TryParseTimeFallback(string raw)
        => ProtocolEntryInputNormalizer.TryParseTimeFallback(raw);

    private static string FormatTime(TimeSpan value)
        => ProtocolEntryInputNormalizer.FormatTime(value);

    // Delegation an DefaultDescriptionBuilder (Application-Schicht)
    private static string BuildDefaultDescription(
        AuswertungPro.Next.Application.Protocol.CodeDefinition def,
        IReadOnlyDictionary<string, string> parameters,
        double? meterStart,
        double? meterEnd)
        => DefaultDescriptionBuilder.Build(def, parameters, meterStart, meterEnd);

    private void SetZeitFromVideo()
    {
        if (!PlayerWindow.TryGetCurrentTime(out var time))
        {
            ValidationStatus.Text = "Kein Video aktiv. Bitte zuerst Video öffnen.";
            return;
        }

        ZeitTextBox.Text = FormatTime(time);
        if (string.IsNullOrWhiteSpace(MpegTextBox.Text))
            MpegTextBox.Text = FormatTime(time);
        ValidationStatus.Text = "Zeit aus Video übernommen.";
    }

    private void SeekToZeit()
    {
        if (!TryParseOptionalTimeSpan(ZeitTextBox.Text, out var zeit) || zeit is null)
        {
            ValidationStatus.Text = "Zeit ist ungültig.";
            return;
        }

        if (!PlayerWindow.TrySeekTo(zeit.Value))
            ValidationStatus.Text = "Video ist nicht aktiv.";
        else
            ValidationStatus.Text = "Video zu Zeitposition gesetzt.";
    }
}
