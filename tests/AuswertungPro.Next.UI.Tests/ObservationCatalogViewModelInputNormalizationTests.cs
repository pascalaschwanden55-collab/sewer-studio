using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObservationCatalogViewModelInputNormalizationTests
{
    [Fact]
    public void Konstruktor_formatiert_Meter_und_Zeit_wie_bisher()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABAC",
            MeterStart = 1.2,
            MeterEnd = null,
            Zeit = new TimeSpan(1, 2, 3)
        };

        var viewModel = CreateViewModel(entry);

        Assert.Equal("1.20", viewModel.MeterStartText);
        Assert.Equal(string.Empty, viewModel.MeterEndText);
        Assert.Equal("01:02:03", viewModel.ZeitText);
    }

    [Fact]
    public void ApplyToEntry_parst_Komma_Punkt_und_Kurzzeit_wie_bisher()
    {
        var entry = new ProtocolEntry { Code = "BABAC" };
        var viewModel = CreateViewModel(entry);
        viewModel.MeterStartText = "1,25";
        viewModel.MeterEndText = "2.50";
        viewModel.ZeitText = "03:04";
        viewModel.IsStreckenschaden = true;

        var applied = viewModel.ApplyToEntry();

        Assert.True(applied, viewModel.ValidationMessage);
        Assert.Equal(1.25, entry.MeterStart);
        Assert.Equal(2.5, entry.MeterEnd);
        Assert.Equal(new TimeSpan(0, 3, 4), entry.Zeit);
    }

    [Fact]
    public void ApplyToEntry_verwendet_Vsa_Distanz_wenn_beide_Meterfelder_leer_sind()
    {
        var entry = new ProtocolEntry { Code = "BABAC" };
        var viewModel = CreateViewModel(entry);
        viewModel.MeterStartText = string.Empty;
        viewModel.MeterEndText = string.Empty;
        viewModel.ZeitText = string.Empty;
        viewModel.VsaDistanz = "3,75";

        var applied = viewModel.ApplyToEntry();

        Assert.True(applied, viewModel.ValidationMessage);
        Assert.Equal(3.75, entry.MeterStart);
        Assert.Equal(3.75, entry.MeterEnd);
        Assert.Null(entry.Zeit);
    }

    [Fact]
    public void ApplyToEntry_ignoriert_ungueltige_optionale_Vsa_Distanz_wie_bisher()
    {
        var entry = new ProtocolEntry { Code = "BABAC" };
        var viewModel = CreateViewModel(entry);
        viewModel.MeterStartText = string.Empty;
        viewModel.MeterEndText = string.Empty;
        viewModel.VsaDistanz = "ungueltig";

        var applied = viewModel.ApplyToEntry();

        Assert.True(applied, viewModel.ValidationMessage);
        Assert.Null(entry.MeterStart);
        Assert.Null(entry.MeterEnd);
    }

    [Theory]
    [InlineData("ungueltig", "2", "03:04", "MeterStart ist ungueltig.")]
    [InlineData("1", "ungueltig", "03:04", "MeterEnd ist ungueltig.")]
    [InlineData("1", "2", "ungueltig", "Zeit ist ungueltig.")]
    public void ApplyToEntry_behaelt_die_bisherige_erste_Fehlermeldung(
        string meterStart,
        string meterEnd,
        string time,
        string expectedMessage)
    {
        var entry = new ProtocolEntry { Code = "BABAC" };
        var viewModel = CreateViewModel(entry);
        viewModel.MeterStartText = meterStart;
        viewModel.MeterEndText = meterEnd;
        viewModel.ZeitText = time;

        var applied = viewModel.ApplyToEntry();

        Assert.False(applied);
        Assert.Equal(expectedMessage, viewModel.ValidationMessage);
    }

    private static ObservationCatalogViewModel CreateViewModel(ProtocolEntry entry)
        => new(new SingleCodeCatalogProvider(), entry);

    private sealed class SingleCodeCatalogProvider : ICodeCatalogProvider
    {
        private static readonly CodeDefinition Definition = new()
        {
            Code = "BABAC",
            Title = "Laengsriss",
            Group = "Kanal"
        };

        public IReadOnlyList<CodeDefinition> GetAll() => [Definition];

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Definition;
            return string.Equals(code, Definition.Code, StringComparison.OrdinalIgnoreCase);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test catalog is read-only.");

        public IReadOnlyList<string> AllowedCodes() => [Definition.Code];

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => [];
    }
}
