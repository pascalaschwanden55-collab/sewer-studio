using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.ViewModels.Protocol;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObservationParameterViewModelTests
{
    [Fact]
    public void Required_parameter_updates_validation_state_when_value_changes()
    {
        var viewModel = new ObservationParameterViewModel(
            new CodeParameter
            {
                Name = "Ausmass",
                Type = "number",
                Unit = "mm",
                Required = true
            },
            existingValue: null);

        Assert.False(viewModel.Validate(out var missingError));
        Assert.False(viewModel.IsValid);
        Assert.Equal(missingError, viewModel.ErrorMessage);

        viewModel.Value = "12,5";

        Assert.True(viewModel.IsValid);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.True(viewModel.Validate(out var validError));
        Assert.Empty(validError);
    }

    [Fact]
    public void Enum_parameter_rejects_unknown_value_and_accepts_catalog_value()
    {
        var viewModel = new ObservationParameterViewModel(
            new CodeParameter
            {
                Name = "Material",
                Type = "enum",
                AllowedValues = ["Beton", "Steinzeug"]
            },
            existingValue: "Holz");

        Assert.False(viewModel.Validate(out _));
        Assert.False(viewModel.IsValid);

        viewModel.Value = "Steinzeug";

        Assert.True(viewModel.IsValid);
        Assert.Empty(viewModel.ErrorMessage);
    }

    [Fact]
    public void Clock_command_trims_selection_and_ignores_empty_input()
    {
        var viewModel = new ObservationParameterViewModel(
            new CodeParameter
            {
                Name = "Uhrposition",
                Type = "clock"
            },
            existingValue: "03");

        viewModel.SelectClockCommand.Execute(" 09 ");
        Assert.Equal("09", viewModel.Value);

        viewModel.SelectClockCommand.Execute("  ");
        Assert.Equal("09", viewModel.Value);
    }
}
