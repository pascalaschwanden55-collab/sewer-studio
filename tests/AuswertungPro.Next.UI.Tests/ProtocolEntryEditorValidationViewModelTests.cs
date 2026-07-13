using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolEntryEditorValidationViewModelTests
{
    [Fact]
    public void Validate_akzeptiert_vollstaendige_gueltige_eingabe()
    {
        var parameters = new ProtocolEntryEditorViewModel(new Catalog(Definition("BAB")));
        var viewModel = new ProtocolEntryEditorValidationViewModel(new Catalog(Definition("BAB")), parameters);

        var result = viewModel.Validate(Input());

        Assert.True(result.IsValid);
        Assert.True(result.CodeOk);
        Assert.True(result.MeterStartOk);
        Assert.True(result.MeterEndOk);
        Assert.True(result.ZeitOk);
        Assert.True(result.StreckenschadenOk);
        Assert.True(result.Vsa.DistanzOk);
        Assert.Empty(result.Errors);
        Assert.Equal("Eingabe gültig.", result.ValidationText);
    }

    [Fact]
    public void Validate_bewahrt_reihenfolge_und_status_der_bisherigen_fehler()
    {
        var catalog = new Catalog(Definition("BAB"));
        var parameters = new ProtocolEntryEditorViewModel(catalog);
        var viewModel = new ProtocolEntryEditorValidationViewModel(catalog, parameters);

        var result = viewModel.Validate(Input(
            code: "ZZZ",
            meterStart: "unlesbar",
            meterEnd: string.Empty,
            zeit: "unlesbar",
            isStreckenschaden: true,
            vsa: new VsaFieldInputs { IsStreckenschaden = true }));

        Assert.False(result.IsValid);
        Assert.False(result.CodeOk);
        Assert.False(result.MeterStartOk);
        Assert.False(result.ZeitOk);
        Assert.False(result.StreckenschadenOk);
        Assert.False(result.Vsa.DistanzOk);
        Assert.Equal("Code ist nicht im Katalog vorhanden.", result.Errors[0]);
        Assert.Contains("MeterStart ist ungueltig.", result.Errors);
        Assert.Contains("Streckenschaden: MeterStart und MeterEnde sind Pflicht.", result.Errors);
        Assert.Contains("VSA: Distanz (m) ist erforderlich.", result.Errors);
        Assert.Contains("Code nicht im Katalog.", result.Errors);
    }

    [Fact]
    public void Validate_nimmt_pflichtparameter_des_codekatalogs_in_gesamtfehler_auf()
    {
        var definition = Definition("BAB");
        definition.Parameters.Add(new CodeParameter
        {
            Name = "Ausmass",
            Type = "number",
            Required = true
        });
        var catalog = new Catalog(definition);
        var parameters = new ProtocolEntryEditorViewModel(catalog);
        var viewModel = new ProtocolEntryEditorValidationViewModel(catalog, parameters);

        var result = viewModel.Validate(Input());

        Assert.False(result.IsValid);
        Assert.Contains("Ausmass *: Pflichtfeld.", result.Errors);
        Assert.Contains("Ausmass *: Pflichtfeld.", result.ValidationText, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateVsaFields_verwendet_die_katalogdefinition_des_codes()
    {
        var catalog = new Catalog(Definition("BAB"));
        var viewModel = new ProtocolEntryEditorValidationViewModel(catalog, parameterEditor: null);

        var result = viewModel.ValidateVsaFields("BAB", new VsaFieldInputs
        {
            Distanz = "1.0",
            Q1 = "25"
        });

        Assert.False(result.Q1Ok);
        Assert.Contains("VSA: Quantifizierung 1 ist fuer diesen Code nicht vorgesehen.", result.Errors);
    }

    [Fact]
    public void ValidateVsaFields_behandelt_unbekannten_code_ohne_leere_katalogdefinition()
    {
        var catalog = new Catalog(Definition("BAB"));
        var viewModel = new ProtocolEntryEditorValidationViewModel(catalog, parameterEditor: null);

        var result = viewModel.ValidateVsaFields("ZZZ", new VsaFieldInputs
        {
            Distanz = "1.0",
            Q1 = "25"
        });

        Assert.True(result.Q1Ok);
        Assert.DoesNotContain(
            "VSA: Quantifizierung 1 ist fuer diesen Code nicht vorgesehen.",
            result.Errors);
    }

    private static ProtocolEntryEditorValidationInput Input(
        string code = "BAB",
        string meterStart = "1.5",
        string meterEnd = "",
        string zeit = "01:02",
        bool isStreckenschaden = false,
        VsaFieldInputs? vsa = null)
        => new(
            Code: code,
            MeterStart: meterStart,
            MeterEnd: meterEnd,
            Zeit: zeit,
            IsStreckenschaden: isStreckenschaden,
            Vsa: vsa ?? new VsaFieldInputs { Distanz = "1.5" });

    private static CodeDefinition Definition(string code)
        => new() { Code = code, IsSelectable = true };

    private sealed class Catalog(params CodeDefinition[] definitions) : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _definitions = definitions;

        public IReadOnlyList<CodeDefinition> GetAll() => _definitions;

        public bool TryGet(string code, out CodeDefinition def)
        {
            var found = _definitions.FirstOrDefault(
                item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
            def = found ?? new CodeDefinition();
            return found is not null;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new NotSupportedException();

        public IReadOnlyList<string> AllowedCodes()
            => _definitions.Where(item => item.IsSelectable).Select(item => item.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }
}
