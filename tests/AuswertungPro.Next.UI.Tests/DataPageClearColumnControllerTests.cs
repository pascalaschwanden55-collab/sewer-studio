using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageClearColumnControllerTests
{
    [Fact]
    public void BuildPlan_reports_already_empty_when_no_record_has_value()
    {
        var records = new[] { new HaltungRecord(), new HaltungRecord() };

        var plan = DataPageClearColumnController.BuildPlan(records, "Bemerkungen");

        Assert.Equal(DataPageClearColumnStatus.AlreadyEmpty, plan.Status);
        Assert.Equal(0, plan.AffectedCount);
        Assert.Equal(2, plan.TotalCount);
    }

    [Fact]
    public void BuildPlan_counts_non_empty_values()
    {
        var first = new HaltungRecord();
        var second = new HaltungRecord();
        first.SetFieldValue("Bemerkungen", "Text", FieldSource.Manual, userEdited: true);

        var plan = DataPageClearColumnController.BuildPlan(new[] { first, second }, "Bemerkungen");

        Assert.Equal(DataPageClearColumnStatus.RequiresConfirmation, plan.Status);
        Assert.Equal(1, plan.AffectedCount);
        Assert.Equal(2, plan.TotalCount);
    }

    [Fact]
    public void ClearColumn_clears_values_and_allows_future_imports()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Bemerkungen", "Manuell", FieldSource.Manual, userEdited: true);

        var cleared = DataPageClearColumnController.ClearColumn(new[] { record }, "Bemerkungen");

        Assert.Equal(1, cleared);
        Assert.Equal(string.Empty, record.GetFieldValue("Bemerkungen"));
        Assert.False(record.FieldMeta["Bemerkungen"].UserEdited);
        Assert.Equal(FieldSource.Manual, record.FieldMeta["Bemerkungen"].Source);
    }
}
