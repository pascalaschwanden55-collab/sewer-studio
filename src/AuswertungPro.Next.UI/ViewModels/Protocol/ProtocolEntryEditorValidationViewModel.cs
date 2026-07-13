using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol;

public sealed record ProtocolEntryEditorValidationInput(
    string Code,
    string MeterStart,
    string MeterEnd,
    string Zeit,
    bool IsStreckenschaden,
    VsaFieldInputs Vsa);

public sealed record ProtocolEntryEditorValidationResult(
    bool CodeOk,
    bool MeterStartOk,
    bool MeterEndOk,
    bool ZeitOk,
    bool StreckenschadenOk,
    VsaValidationResult Vsa,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public string ValidationText => IsValid
        ? "Eingabe gültig."
        : string.Join(Environment.NewLine, Errors.Take(12));
}

/// <summary>
/// Setzt die vorhandenen Eingabe-, Katalog- und VSA-Prüfungen für den Protokoll-Editor
/// zu einem strukturierten Ergebnis zusammen. Die WPF-View färbt damit nur noch Felder ein.
/// </summary>
public sealed class ProtocolEntryEditorValidationViewModel
{
    private readonly ICodeCatalogProvider? _catalog;
    private readonly ProtocolEntryEditorViewModel? _parameterEditor;

    public ProtocolEntryEditorValidationViewModel(
        ICodeCatalogProvider? catalog,
        ProtocolEntryEditorViewModel? parameterEditor)
    {
        _catalog = catalog;
        _parameterEditor = parameterEditor;
    }

    public ProtocolEntryEditorValidationResult Validate(ProtocolEntryEditorValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Vsa);

        var errors = new List<string>();
        var code = (input.Code ?? string.Empty).Trim();
        var codeOk = !string.IsNullOrWhiteSpace(code);
        if (!codeOk)
        {
            errors.Add("Code ist erforderlich.");
        }
        else if (_catalog is null || !_catalog.TryGet(code, out _))
        {
            codeOk = false;
            errors.Add("Code ist nicht im Katalog vorhanden.");
        }

        var meterStartOk = ProtocolEntryInputNormalizer.TryParseOptionalDouble(
            input.MeterStart ?? string.Empty,
            out var meterStart);
        var meterEndOk = ProtocolEntryInputNormalizer.TryParseOptionalDouble(
            input.MeterEnd ?? string.Empty,
            out var meterEnd);
        if (!meterStartOk)
            errors.Add("MeterStart ist ungueltig.");
        if (!meterEndOk)
            errors.Add("MeterEnd ist ungueltig.");

        var zeitOk = ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(
            input.Zeit ?? string.Empty,
            out _);
        if (!zeitOk)
            errors.Add("Zeit ist ungueltig.");

        var streckenschadenOk = true;
        if (input.IsStreckenschaden)
        {
            if (!meterStart.HasValue || !meterEnd.HasValue)
            {
                streckenschadenOk = false;
                errors.Add("Streckenschaden: MeterStart und MeterEnde sind Pflicht.");
            }
            else if (meterEnd < meterStart)
            {
                streckenschadenOk = false;
                errors.Add("Streckenschaden: MeterEnde muss groesser/gleich MeterStart sein.");
            }
        }

        var vsaInputs = input.Vsa with { IsStreckenschaden = input.IsStreckenschaden };
        var vsa = ValidateVsaFields(code, vsaInputs);
        errors.AddRange(vsa.Errors);

        if (_parameterEditor is not null)
        {
            if (!string.Equals(_parameterEditor.Code, code, StringComparison.OrdinalIgnoreCase))
                _parameterEditor.Code = code;

            _parameterEditor.Validate();
            if (!_parameterEditor.IsValid)
                errors.AddRange(_parameterEditor.ValidationMessages);
        }

        var uniqueErrors = errors
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProtocolEntryEditorValidationResult(
            codeOk,
            meterStartOk,
            meterEndOk,
            zeitOk,
            streckenschadenOk,
            vsa,
            uniqueErrors);
    }

    public VsaValidationResult ValidateVsaFields(string code, VsaFieldInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        CodeDefinition? catalogDefinition = null;
        if (_catalog is not null
            && !string.IsNullOrWhiteSpace(code)
            && _catalog.TryGet(code, out var foundDefinition))
        {
            catalogDefinition = foundDefinition;
        }

        return ProtocolEntryValidator.ValidateVsaFields(
            code,
            inputs,
            catalogDefinition,
            requireDistanz: true);
    }
}
