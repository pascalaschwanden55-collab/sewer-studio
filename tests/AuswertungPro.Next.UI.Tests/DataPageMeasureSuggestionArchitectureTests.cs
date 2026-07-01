using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageMeasureSuggestionArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_massnahmenvorschlaege_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        var suggestOne = ExtractMethod(source, "private void SuggestMeasures(HaltungRecord? record)");
        var suggestAll = ExtractMethod(source, "public void SuggestAllMeasures()");

        Assert.Contains("_measureSuggestionController.Suggest(record);", suggestOne, StringComparison.Ordinal);
        Assert.DoesNotContain("_measureRecommendationService.Recommend", suggestOne, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageSanierungCostMapper.ApplyRecommendation", suggestOne, StringComparison.Ordinal);
        Assert.DoesNotContain("_sp.Dialogs.Info(", suggestOne, StringComparison.Ordinal);

        Assert.Contains("_measureSuggestionController.SuggestAll();", suggestAll, StringComparison.Ordinal);
        Assert.DoesNotContain("_measureRecommendationService.Recommend", suggestAll, StringComparison.Ordinal);
        Assert.DoesNotContain("DataPageSanierungCostMapper.ApplyRecommendation", suggestAll, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var record in records)", suggestAll, StringComparison.Ordinal);
    }

}
