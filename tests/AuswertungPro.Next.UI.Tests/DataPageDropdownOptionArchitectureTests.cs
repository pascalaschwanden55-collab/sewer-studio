using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageDropdownOptionArchitectureTests
{
    [Fact]
    public void ViewModel_delegiert_Datensatz_Synchronisierung_und_Textzerlegung()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.DropdownOptions.cs"));

        Assert.Contains("DataPageDropdownOptionSynchronizer.SyncFromRecords(", viewModel);
        Assert.Contains("DataPageDropdownOptionSynchronizer.ParseRecommendedTemplates(raw)", viewModel);
        Assert.DoesNotContain("foreach (var record in Records)", viewModel);
        Assert.DoesNotContain("raw.Split(", viewModel);
    }

    [Fact]
    public void ViewModel_verwendet_gemeinsamen_Controller_fuer_alle_fuenf_Listen()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.DropdownOptions.cs"));

        Assert.Contains("DataPageDropdownOptionGroupFactory.Create(", viewModel);
        foreach (var group in new[]
                 {
                     "Sanieren",
                     "Eigentuemer",
                     "Pruefungsresultat",
                     "Referenzpruefung",
                     "EmpfohleneSanierungsmassnahmen"
                 })
        {
            Assert.Contains($"=> OptionGroups.{group}.Edit();", viewModel);
            Assert.Contains($"=> OptionGroups.{group}.Preview();", viewModel);
            Assert.Contains($"=> OptionGroups.{group}.Reset();", viewModel);
            Assert.Contains($"=> OptionGroups.{group}.Add(value);", viewModel);
            Assert.Contains($"=> OptionGroups.{group}.Remove(value);", viewModel);
        }

        Assert.DoesNotContain("new OptionsEditorViewModel", viewModel);
        Assert.DoesNotContain("new OptionsEditorWindow", viewModel);
        Assert.DoesNotContain("lockedToResetItems: true", viewModel);
        Assert.DoesNotContain("private void RemoveOptionFromList", viewModel);
    }
}
