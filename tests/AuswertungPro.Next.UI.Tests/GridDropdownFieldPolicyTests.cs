using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class GridDropdownFieldPolicyTests
{
    [Theory]
    [InlineData("Sanieren_JaNein", "SanierenOptions", true, true, "EditSanierenOptionsCommand")]
    [InlineData("Eigentuemer", "EigentuemerOptions", false, true, "EditEigentuemerOptionsCommand")]
    [InlineData("Pruefungsresultat", "PruefungsresultatOptions", true, true, "EditPruefungsresultatOptionsCommand")]
    [InlineData("Referenzpruefung", "ReferenzpruefungOptions", true, true, "EditReferenzpruefungOptionsCommand")]
    public void TryResolve_returns_managed_combo_specs(
        string field,
        string itemsSourcePath,
        bool allowFreeText,
        bool managed,
        string editCommand)
    {
        var ok = GridDropdownFieldPolicy.TryResolve(field, out var spec);

        Assert.True(ok);
        Assert.Equal(field, spec.OptionField);
        Assert.Equal(itemsSourcePath, spec.ItemsSourcePath);
        Assert.Equal(allowFreeText, spec.AllowFreeText);
        Assert.Equal(managed, spec.Managed);
        Assert.Equal(editCommand, spec.EditCommand);
        Assert.NotEmpty(spec.PreviewCommand);
        Assert.NotEmpty(spec.ResetCommand);
        Assert.NotEmpty(spec.RemoveCommand);
        Assert.NotEmpty(spec.AddCommand);
    }

    [Fact]
    public void TryResolve_returns_simple_combo_for_ausgefuehrt_durch()
    {
        var ok = GridDropdownFieldPolicy.TryResolve("Ausgefuehrt_durch", out var spec);

        Assert.True(ok);
        Assert.False(spec.Managed);
        Assert.True(spec.AllowFreeText);
        Assert.Equal("AusgefuehrtDurchOptions", spec.ItemsSourcePath);
        Assert.Equal("", spec.EditCommand);
    }

    [Fact]
    public void TryResolve_returns_fixed_combo_for_schachtform()
    {
        var ok = GridDropdownFieldPolicy.TryResolve("Schachtform", out var spec);

        Assert.True(ok);
        Assert.False(spec.Managed);
        Assert.False(spec.AllowFreeText);
        Assert.Equal("SchachtformOptions", spec.ItemsSourcePath);
    }

    [Fact]
    public void TryResolve_returns_false_for_unknown_field()
    {
        var ok = GridDropdownFieldPolicy.TryResolve("Unbekannt", out var spec);

        Assert.False(ok);
        Assert.Null(spec);
    }
}
