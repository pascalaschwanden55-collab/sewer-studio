using System.Collections.ObjectModel;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DropdownOptionGroupControllerTests
{
    [Fact]
    public void Edit_replaces_options_and_saves_when_dialog_is_accepted()
    {
        var options = new ObservableCollection<string> { "Alt" };
        var saveCalls = 0;
        var controller = Create(
            options,
            editOptions: current =>
            {
                Assert.Equal(new[] { "Alt" }, current);
                return new DropdownOptionEditorResult(true, new[] { "Neu", "Alt" });
            },
            save: () => saveCalls++);

        controller.Edit();

        Assert.Equal(new[] { "Neu", "Alt" }, options);
        Assert.Equal(1, saveCalls);
    }

    [Fact]
    public void Edit_keeps_options_without_save_when_dialog_is_cancelled()
    {
        var options = new ObservableCollection<string> { "Alt" };
        var saveCalls = 0;
        var controller = Create(
            options,
            editOptions: _ => new DropdownOptionEditorResult(false, new[] { "Neu" }),
            save: () => saveCalls++);

        controller.Edit();

        Assert.Equal(new[] { "Alt" }, options);
        Assert.Equal(0, saveCalls);
    }

    [Fact]
    public void Preview_shows_joined_items_with_configured_title()
    {
        var options = new ObservableCollection<string> { "A", "B" };
        var info = "";
        var title = "";
        var controller = Create(
            options,
            showInfo: (message, caption) =>
            {
                info = message;
                title = caption;
            });

        controller.Preview();

        Assert.Equal("A\nB", info);
        Assert.Equal("Schacht-Liste", title);
    }

    [Fact]
    public void Reset_add_and_remove_update_options_and_save_only_on_change()
    {
        var options = new ObservableCollection<string> { "Alt" };
        var saveCalls = 0;
        var controller = Create(options, save: () => saveCalls++);

        controller.Reset();
        controller.Add("  Ja  ");
        controller.Add("ja");
        controller.Remove("nein");
        controller.Remove("fehlt");

        Assert.Equal(new[] { "Ja" }, options);
        Assert.Equal(3, saveCalls);
    }

    [Fact]
    public void Locked_group_ignores_edit_add_remove_values_and_persists_fixed_items()
    {
        var options = new ObservableCollection<string> { "Privat", "Falsch" };
        var saveCalls = 0;
        var fixedItems = new[] { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };
        var controller = Create(
            options,
            resetItems: fixedItems,
            lockedToResetItems: true,
            editOptions: _ => new DropdownOptionEditorResult(true, new[] { "Beliebig" }),
            save: () => saveCalls++);

        controller.Edit();
        controller.Add("Noch eins");
        controller.Remove("Privat");

        Assert.Equal(fixedItems, options);
        Assert.Equal(3, saveCalls);
    }

    [Fact]
    public void Unlocked_group_does_not_save_for_empty_duplicate_or_missing_values()
    {
        var options = new ObservableCollection<string> { "Privat" };
        var saveCalls = 0;
        var controller = Create(
            options,
            resetItems: new[] { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" },
            save: () => saveCalls++);

        controller.Add(null);
        controller.Add("   ");
        controller.Add("privat");
        controller.Remove(null);
        controller.Remove("fehlt");

        Assert.Equal(new[] { "Privat" }, options);
        Assert.Equal(0, saveCalls);
    }

    private static DropdownOptionGroupController Create(
        ObservableCollection<string> options,
        IReadOnlyList<string>? resetItems = null,
        bool lockedToResetItems = false,
        Func<IReadOnlyList<string>, DropdownOptionEditorResult>? editOptions = null,
        Action<string, string>? showInfo = null,
        Action? save = null)
        => new(
            options,
            new DropdownOptionGroupSettings(
                PreviewTitle: "Schacht-Liste",
                ResetItems: resetItems ?? new[] { "Nein" },
                LockedToResetItems: lockedToResetItems),
            new DropdownOptionGroupActions(
                EditOptions: editOptions ?? (_ => new DropdownOptionEditorResult(true, Array.Empty<string>())),
                ShowInfo: showInfo ?? ((_, _) => { }),
                Save: save ?? (() => { })));
}
