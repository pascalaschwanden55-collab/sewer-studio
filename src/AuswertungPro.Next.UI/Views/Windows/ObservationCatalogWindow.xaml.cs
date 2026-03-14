using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class ObservationCatalogWindow : Window
{
    private readonly ObservationCatalogViewModel _vm;

    public ObservationCatalogWindow(ObservationCatalogViewModel vm)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        _vm = vm;
        DataContext = vm;

        ApplyButton.Click += (_, _) => ApplyAndClose();
        ApplyButton.ToolTip = "Shortcut: Ctrl+S";
        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        KiSuggestButton.Click += async (_, _) => await SuggestWithKiAsync();
        KiSuggestButton.ToolTip = "Shortcut: Ctrl+L";
        SearchTextBox.ToolTip = "Shortcut: Ctrl+F";

        HookVsaValidationEvents();
        HookParameterValidationEvents();
        _vm.Parameters.CollectionChanged += Parameters_CollectionChanged;
        _vm.PropertyChanged += Vm_PropertyChanged;
        PreviewKeyDown += OnWindowPreviewKeyDown;
        Closed += OnWindowClosed;

        ApplyLiveValidation();
    }

    private void ApplyAndClose()
    {
        NormalizeAllStrictInputs();
        if (!ApplyLiveValidation(forceMessage: true))
            return;

        if (!_vm.ApplyToEntry())
        {
            ApplyLiveValidation(forceMessage: true);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void SearchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchList.SelectedItem is not AuswertungPro.Next.Application.Protocol.CodeDefinition code)
            return;

        _vm.SelectCode(code, syncColumns: true);
    }

    private void Column_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox list || list.SelectedItem is not CatalogItem item)
            return;

        if (list.DataContext is not CatalogColumnViewModel column)
            return;

        _vm.SelectColumnItem(column.Index, item);
    }

    private async Task SuggestWithKiAsync()
    {
        if (_vm.IsKiBusy)
            return;

        KiSuggestButton.IsEnabled = false;
        try
        {
            await _vm.SuggestCodeWithKiAsync();
        }
        finally
        {
            _ = Dispatcher.BeginInvoke(() => KiSuggestButton.IsEnabled = true, DispatcherPriority.Background);
            ApplyLiveValidation();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _vm.Parameters.CollectionChanged -= Parameters_CollectionChanged;
        _vm.PropertyChanged -= Vm_PropertyChanged;
        PreviewKeyDown -= OnWindowPreviewKeyDown;

        foreach (var parameter in _vm.Parameters)
            parameter.PropertyChanged -= Parameter_PropertyChanged;
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

    private void HookParameterValidationEvents()
    {
        foreach (var parameter in _vm.Parameters)
        {
            parameter.PropertyChanged -= Parameter_PropertyChanged;
            parameter.PropertyChanged += Parameter_PropertyChanged;
        }
    }

    private void Parameters_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<ObservationParameterViewModel>())
                item.PropertyChanged -= Parameter_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<ObservationParameterViewModel>())
            {
                item.PropertyChanged -= Parameter_PropertyChanged;
                item.PropertyChanged += Parameter_PropertyChanged;
            }
        }

        ApplyLiveValidation();
    }

    private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ObservationParameterViewModel.Value)
            or nameof(ObservationParameterViewModel.IsValid)
            or nameof(ObservationParameterViewModel.ErrorMessage))
        {
            ApplyLiveValidation();
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ObservationCatalogViewModel.SelectedCode)
            or nameof(ObservationCatalogViewModel.IsStreckenschaden)
            or nameof(ObservationCatalogViewModel.IsKiBusy)
            or nameof(ObservationCatalogViewModel.VsaUhrVon)
            or nameof(ObservationCatalogViewModel.VsaUhrBis)
            or nameof(ObservationCatalogViewModel.VsaDistanz)
            or nameof(ObservationCatalogViewModel.VsaVideo)
            or nameof(ObservationCatalogViewModel.VsaQ1)
            or nameof(ObservationCatalogViewModel.VsaQ2)
            or nameof(ObservationCatalogViewModel.VsaStrecke)
            or nameof(ObservationCatalogViewModel.VsaEz)
            or nameof(ObservationCatalogViewModel.VsaSchachtbereich))
        {
            ApplyLiveValidation();
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            ApplyAndClose();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.L)
        {
            e.Handled = true;
            _ = SuggestWithKiAsync();
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            e.Handled = true;
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None || e.Key != Key.Enter)
            return;

        if (Keyboard.FocusedElement is TextBox { AcceptsReturn: true })
            return;

        e.Handled = true;
        (Keyboard.FocusedElement as UIElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private bool ApplyLiveValidation(bool forceMessage = false)
    {
        var errors = new List<string>();

        var codeSelected = _vm.SelectedCode is not null;
        if (!codeSelected)
            errors.Add("Bitte einen Code auswaehlen.");

        foreach (var parameter in _vm.Parameters)
        {
            if (!parameter.Validate(out var error))
                errors.Add(error);
        }

        var vsaErrors = ValidateVsaUiFields(
            codeSelected,
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

        if (uniqueErrors.Count > 0 || forceMessage)
            _vm.ValidationMessage = uniqueErrors.Count == 0 ? string.Empty : string.Join(Environment.NewLine, uniqueErrors.Take(12));

        ApplyButton.IsEnabled = uniqueErrors.Count == 0 && !_vm.IsKiBusy;
        return uniqueErrors.Count == 0;
    }

    private List<string> ValidateVsaUiFields(
        bool codeSelected,
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
        var errors = new List<string>();

        vsaDistanzOk = TryParseOptionalDouble(VsaDistanzTextBox.Text, out var distanz);
        if (!vsaDistanzOk)
            errors.Add("VSA: Distanz ist ungueltig.");
        else if (codeSelected && !distanz.HasValue)
        {
            vsaDistanzOk = false;
            errors.Add("VSA: Distanz (m) ist erforderlich.");
        }

        vsaVideoOk = TryParseOptionalTimeSpan(VsaVideoTextBox.Text, out _);
        if (!vsaVideoOk)
            errors.Add("VSA: Uhrzeit (Video) ist ungueltig.");

        vsaUhrVonOk = TryNormalizeClockPosition(VsaUhrVonComboBox.Text, out _, out var hasUhrVon);
        if (!vsaUhrVonOk)
            errors.Add("VSA: Uhrzeit von nur 00 bis 12.");

        vsaUhrBisOk = TryNormalizeClockPosition(VsaUhrBisComboBox.Text, out _, out var hasUhrBis);
        if (!vsaUhrBisOk)
            errors.Add("VSA: Uhrzeit bis nur 00 bis 12.");

        vsaQ1Ok = TryParseOptionalDouble(VsaQ1TextBox.Text, out _);
        if (!vsaQ1Ok)
            errors.Add("VSA: Quantifizierung 1 ist ungueltig.");

        vsaQ2Ok = TryParseOptionalDouble(VsaQ2TextBox.Text, out _);
        if (!vsaQ2Ok)
            errors.Add("VSA: Quantifizierung 2 ist ungueltig.");

        vsaStreckeOk = TryNormalizeStrecke(VsaStreckeTextBox.Text, out _, out var hasStrecke);
        if (!vsaStreckeOk)
            errors.Add("VSA: Strecke nur im Format A1/B1/C1...");
        if (_vm.IsStreckenschaden && !hasStrecke)
        {
            vsaStreckeOk = false;
            errors.Add("VSA: Strecke ist bei Streckenschaden erforderlich.");
        }

        vsaEzOk = TryNormalizeEz(VsaEzComboBox.Text, out _, out _);
        if (!vsaEzOk)
            errors.Add("VSA: EZ nur EZ0 bis EZ4.");

        vsaSchachtbereichOk = TryNormalizeSchachtbereich(VsaSchachtbereichTextBox.Text, out _, out _);
        if (!vsaSchachtbereichOk)
            errors.Add("VSA: Schachtbereich nur A/B/D/F/H/I/J.");

        if (_vm.SelectedCode is not null)
        {
            var hasClock = _vm.SelectedCode.Parameters.Any(p =>
                string.Equals(p.Type, "clock", StringComparison.OrdinalIgnoreCase));
            if (hasClock && !hasUhrVon)
            {
                vsaUhrVonOk = false;
                errors.Add("VSA: Uhr von ist erforderlich.");
            }

            var hasQuant1 = _vm.SelectedCode.Parameters.Any(p =>
                string.Equals(p.Name, "Quant1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "Quantifizierung 1", StringComparison.OrdinalIgnoreCase));
            var hasQuant2 = _vm.SelectedCode.Parameters.Any(p =>
                string.Equals(p.Name, "Quant2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "Quantifizierung 2", StringComparison.OrdinalIgnoreCase));

            if (!hasQuant1 && !string.IsNullOrWhiteSpace(VsaQ1TextBox.Text))
            {
                vsaQ1Ok = false;
                errors.Add("VSA: Quantifizierung 1 ist fuer diesen Code nicht vorgesehen.");
            }

            if (!hasQuant2 && !string.IsNullOrWhiteSpace(VsaQ2TextBox.Text))
            {
                vsaQ2Ok = false;
                errors.Add("VSA: Quantifizierung 2 ist fuer diesen Code nicht vorgesehen.");
            }
        }

        if (hasUhrBis && !hasUhrVon)
        {
            vsaUhrVonOk = false;
            errors.Add("VSA: Uhr von ist erforderlich, wenn Uhr bis gesetzt ist.");
        }

        return errors;
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

    private void NormalizeAllStrictInputs()
    {
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

        var normalized = value.HasValue
            ? (value.Value.TotalHours >= 1 ? value.Value.ToString(@"hh\:mm\:ss") : value.Value.ToString(@"mm\:ss"))
            : string.Empty;
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

    private static bool TryParseOptionalDouble(string raw, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var normalized = raw.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return false;

        value = parsed;
        return true;
    }

    private static bool TryParseOptionalTimeSpan(string raw, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryNormalizeClockPosition(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim();
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;
        if (value < 0 || value > 12)
            return false;

        normalized = value.ToString("00", CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryNormalizeStrecke(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim().ToUpperInvariant();
        if (text.Length == 1 && (text == "A" || text == "B" || text == "C"))
        {
            normalized = text + "1";
            return true;
        }

        if (text.Length >= 2 && (text[0] == 'A' || text[0] == 'B' || text[0] == 'C') && text.Skip(1).All(char.IsDigit))
        {
            normalized = text;
            return true;
        }

        return false;
    }

    private static bool TryNormalizeEz(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        var text = raw!.Trim().ToUpperInvariant();
        if (text.StartsWith("EZ", StringComparison.Ordinal))
            text = text.Substring(2);

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return false;
        if (value < 0 || value > 4)
            return false;

        normalized = $"EZ{value}";
        return true;
    }

    private static bool TryNormalizeSchachtbereich(string? raw, out string normalized, out bool hasValue)
    {
        normalized = string.Empty;
        hasValue = !string.IsNullOrWhiteSpace(raw);
        if (!hasValue)
            return true;

        normalized = raw!.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "D" or "F" or "H" or "I" or "J";
    }
}
