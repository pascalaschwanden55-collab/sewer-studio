using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPageDropdownArchitectureTests
{
    [Fact]
    public void ViewModel_behaelt_Command_Fassade_und_delegiert_Dropdown_Ausfuehrung()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "ProjectPageViewModel.cs");
        var factoryPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ProjectPage",
            "ProjectPageDropdownCommandFactory.cs");
        var viewModel = File.ReadAllText(viewModelPath);

        Assert.True(File.Exists(factoryPath), factoryPath);
        Assert.Contains("ProjectPageDropdownCommandFactory.Create(", viewModel);
        Assert.Contains("OptionsEditorDialogService.Show", viewModel);
        Assert.Contains("_dialogs.Info,", viewModel);
        Assert.Contains("SaveDropdownOptions));", viewModel);
        Assert.All(
            new[]
            {
                "EditSanierenOptionsCommand = dropdownCommands.Sanieren.Edit;",
                "PreviewSanierenOptionsCommand = dropdownCommands.Sanieren.Preview;",
                "ResetSanierenOptionsCommand = dropdownCommands.Sanieren.Reset;",
                "AddSanierenOptionCommand = dropdownCommands.Sanieren.Add;",
                "RemoveSanierenOptionCommand = dropdownCommands.Sanieren.Remove;",
                "EditEigentuemerOptionsCommand = dropdownCommands.Eigentuemer.Edit;",
                "PreviewEigentuemerOptionsCommand = dropdownCommands.Eigentuemer.Preview;",
                "ResetEigentuemerOptionsCommand = dropdownCommands.Eigentuemer.Reset;",
                "AddEigentuemerOptionCommand = dropdownCommands.Eigentuemer.Add;",
                "RemoveEigentuemerOptionCommand = dropdownCommands.Eigentuemer.Remove;"
            },
            assignment => Assert.Contains(assignment, viewModel));
        Assert.DoesNotContain("new OptionsEditorViewModel", viewModel);
        Assert.DoesNotContain("new OptionsEditorWindow", viewModel);
        Assert.DoesNotContain("private void EditSanierenOptions", viewModel);
        Assert.DoesNotContain("private void EditEigentuemerOptions", viewModel);

        var saveStart = viewModel.IndexOf("private void SaveDropdownOptions", StringComparison.Ordinal);
        var saveEnd = viewModel.IndexOf(
            "private void EnforceEigentuemerOptionsExact",
            saveStart,
            StringComparison.Ordinal);
        Assert.True(saveStart >= 0 && saveEnd > saveStart);
        var saveMethod = viewModel[saveStart..saveEnd];
        Assert.Contains("EnforceEigentuemerOptionsExact();", saveMethod);
        Assert.Contains("_dropdownOptions.SaveSanierenOptions(SanierenOptions);", saveMethod);
        Assert.Contains("_dropdownOptions.SaveEigentuemerOptions(EigentuemerOptions);", saveMethod);
    }
}
