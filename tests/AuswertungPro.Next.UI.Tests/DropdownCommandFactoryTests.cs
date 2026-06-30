using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownCommandFactoryTests
{
    [Fact]
    public void Create_verknuepft_edit_preview_reset_add_remove()
    {
        var calls = new List<string>();

        var group = DropdownCommandFactory.Create(new DropdownCommandActions(
            Edit: () => calls.Add("edit"),
            Preview: () => calls.Add("preview"),
            Reset: () => calls.Add("reset"),
            Add: value => calls.Add($"add:{value}"),
            Remove: value => calls.Add($"remove:{value}")));

        group.Edit.Execute(null);
        group.Preview.Execute(null);
        group.Reset.Execute(null);
        group.Add.Execute("neu");
        group.Remove.Execute("alt");

        Assert.Equal(
            new[] { "edit", "preview", "reset", "add:neu", "remove:alt" },
            calls);
    }
}
