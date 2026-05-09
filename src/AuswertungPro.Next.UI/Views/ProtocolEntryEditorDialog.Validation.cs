using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views;

// ProtocolEntryEditorDialog Validation + Normalisierung der UI-Felder:
// Live-Validation mit roter Umrandung bei Fehler, Normalisierung von
// Number/Time/Strecke/ClockCombo/EzCombo/Schachtbereich, plus VSA-Feld-
// Validierung. Aus dem Hauptdatei extrahiert (Slice 28).
public partial class ProtocolEntryEditorDialog
{
    private void ApplyLiveValidation()
    {
        var errors = new List<string>();

        var code = (CodeTextBox.Text ?? string.Empty).Trim();
        var hasCatalog = _codeCatalog is not null;
        var codeOk = !string.IsNullOrWhiteSpace(code);
        var codeInCatalog = false;
        if (!codeOk)
            errors.Add("Code ist erforderlich.");
        else if (!hasCatalog || !_codeCatalog!.TryGet(code, out _))
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
            ValidationStatus.Text = "Eingabe gueltig.";
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
        var errors = new List<string>();
        var hasCode = !string.IsNullOrWhiteSpace(code);

        vsaDistanzOk = TryParseOptionalDouble(VsaDistanzTextBox.Text, out var distanz);
        if (!vsaDistanzOk)
            errors.Add("VSA: Distanz ist ungueltig.");
        else if (hasCode && !distanz.HasValue)
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
        if (StreckenschadenCheckBox.IsChecked == true && !hasStrecke)
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

        if (codeInCatalog && _codeCatalog is not null && _codeCatalog!.TryGet(code, out var def))
        {
            var hasClock = def.Parameters.Any(p => string.Equals(p.Type, "clock", StringComparison.OrdinalIgnoreCase));
            if (hasClock && !hasUhrVon)
            {
                vsaUhrVonOk = false;
                errors.Add("VSA: Uhr von ist erforderlich.");
            }

            var hasQuant1 = def.Parameters.Any(p => string.Equals(p.Name, "Quant1", StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(p.Name, "Quantifizierung 1", StringComparison.OrdinalIgnoreCase));
            var hasQuant2 = def.Parameters.Any(p => string.Equals(p.Name, "Quant2", StringComparison.OrdinalIgnoreCase)
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
}
