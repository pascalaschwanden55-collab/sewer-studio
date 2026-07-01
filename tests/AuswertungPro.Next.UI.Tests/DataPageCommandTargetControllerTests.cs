using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCommandTargetControllerTests
{
    [Fact]
    public void HasTarget_ist_true_wenn_command_record_oder_selected_existiert()
    {
        var commandRecord = new HaltungRecord();
        var selected = new HaltungRecord();

        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord, selected: null));
        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord: null, selected));
        Assert.True(DataPageCommandTargetController.HasTarget(commandRecord, selected));
    }

    [Fact]
    public void HasTarget_ist_false_ohne_command_record_und_ohne_selected()
        => Assert.False(DataPageCommandTargetController.HasTarget(commandRecord: null, selected: null));

    [Fact]
    public void DataPageViewModel_delegiert_command_target_pruefungen()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));

        AssertDelegates(source, "private bool CanOpenCosts(HaltungRecord? record)");
        AssertDelegates(source, "private bool CanRestoreCosts(HaltungRecord? record)");
        AssertDelegates(source, "private bool CanSuggestMeasures(HaltungRecord? record)");
    }

    private static void AssertDelegates(string source, string signature)
    {
        var body = ExtractMethodBody(source, signature);

        Assert.Contains("DataPageCommandTargetController.HasTarget(record, Selected)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("if (record is not null)", body, StringComparison.Ordinal);
    }
}
