using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

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
}
