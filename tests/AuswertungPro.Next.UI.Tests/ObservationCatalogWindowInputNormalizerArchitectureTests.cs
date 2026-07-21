using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ObservationCatalogWindowInputNormalizerArchitectureTests
{
    [Fact]
    public void Fenster_nutzt_zentrale_Protokoll_Normalisierung_und_behaelt_UI_Ablauf()
    {
        var windowPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "ObservationCatalogWindow.xaml.cs");
        var window = File.ReadAllText(windowPath);

        Assert.All(
            new[]
            {
                "ProtocolEntryInputNormalizer.TryParseOptionalDouble(",
                "ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(",
                "ProtocolEntryInputNormalizer.TryNormalizeClockPosition(",
                "ProtocolEntryInputNormalizer.TryNormalizeStrecke(",
                "ProtocolEntryInputNormalizer.TryNormalizeEz(",
                "ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich("
            },
            call => Assert.Contains(call, window));

        Assert.DoesNotContain("private static bool TryParseOptionalDouble", window);
        Assert.DoesNotContain("private static bool TryParseOptionalTimeSpan", window);
        Assert.DoesNotContain("private static bool TryNormalizeClockPosition", window);
        Assert.DoesNotContain("private static bool TryNormalizeStrecke", window);
        Assert.DoesNotContain("private static bool TryNormalizeEz", window);
        Assert.DoesNotContain("private static bool TryNormalizeSchachtbereich", window);
        Assert.DoesNotContain("double.TryParse(", window);
        Assert.DoesNotContain("TimeSpan.TryParse", window);

        var applyAndClose = GetBlock(
            window,
            "private void ApplyAndClose",
            "private void SearchList_SelectionChanged");
        AssertInOrder(
            applyAndClose,
            "NormalizeAllStrictInputs();",
            "ApplyLiveValidation(forceMessage: true)",
            "_vm.ApplyToEntry()",
            "ApplyLiveValidation(forceMessage: true)",
            "DialogResult = true;",
            "Close();");

        var liveValidation = GetBlock(
            window,
            "private bool ApplyLiveValidation",
            "private List<string> ValidateVsaUiFields");
        AssertInOrder(
            liveValidation,
            "ValidateVsaUiFields(",
            "errors.AddRange(vsaErrors);",
            "SetControlValidationState(VsaDistanzTextBox",
            "SetControlValidationState(VsaSchachtbereichTextBox",
            "ApplyButton.IsEnabled = uniqueErrors.Count == 0 && !_vm.IsKiBusy;",
            "return uniqueErrors.Count == 0;");
    }

    [Fact]
    public void ViewModel_nutzt_zentrale_Zahlen_und_Zeit_Normalisierung()
    {
        var viewModelPath = RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Protocol",
            "ObservationCatalogViewModel.cs");
        var viewModel = File.ReadAllText(viewModelPath);

        var constructor = GetBlock(
            viewModel,
            "public ObservationCatalogViewModel(",
            "partial void OnSearchTextChanged");
        AssertInOrder(
            constructor,
            "ProtocolEntryInputNormalizer.FormatDouble(_entryVm.MeterStart)",
            "ProtocolEntryInputNormalizer.FormatDouble(_entryVm.MeterEnd)",
            "ProtocolEntryInputNormalizer.FormatTime(_entryVm.Zeit.Value)");

        var apply = GetBlock(
            viewModel,
            "public bool ApplyToEntry()",
            "private void ApplySearchFilter");
        AssertInOrder(
            apply,
            "ProtocolEntryInputNormalizer.TryParseOptionalDouble(MeterStartText, out var meterStart)",
            "ValidationMessage = \"MeterStart ist ungueltig.\";",
            "ProtocolEntryInputNormalizer.TryParseOptionalDouble(MeterEndText, out var meterEnd)",
            "ValidationMessage = \"MeterEnd ist ungueltig.\";",
            "ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(ZeitText, out var zeit)",
            "ValidationMessage = \"Zeit ist ungueltig.\";",
            "ProtocolEntryInputNormalizer.TryParseOptionalDouble(",
            "VsaDistanz ?? string.Empty,",
            "out var vsaDistanz)");

        Assert.Equal(3, CountOccurrences(viewModel, "ProtocolEntryInputNormalizer.TryParseOptionalDouble("));
        Assert.Equal(1, CountOccurrences(viewModel, "ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan("));
        Assert.Equal(2, CountOccurrences(viewModel, "ProtocolEntryInputNormalizer.FormatDouble("));
        Assert.Equal(1, CountOccurrences(viewModel, "ProtocolEntryInputNormalizer.FormatTime("));

        Assert.DoesNotContain("private static bool TryParseOptionalDouble", viewModel);
        Assert.DoesNotContain("private static bool TryParseOptionalTimeSpan", viewModel);
        Assert.DoesNotContain("private static string FormatDouble", viewModel);
        Assert.DoesNotContain("private static string FormatTime", viewModel);
    }

    private static string GetBlock(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{startMarker}' wurde nicht gefunden.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"'{endMarker}' wurde nicht nach '{startMarker}' gefunden.");
        return source[start..end];
    }

    private static void AssertInOrder(string text, params string[] expectedParts)
    {
        var previousIndex = -1;
        foreach (var part in expectedParts)
        {
            var currentIndex = text.IndexOf(part, previousIndex + 1, StringComparison.Ordinal);
            Assert.True(currentIndex > previousIndex, $"'{part}' steht nicht an der erwarteten Stelle.");
            previousIndex = currentIndex;
        }
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
