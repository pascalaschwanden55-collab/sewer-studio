using AuswertungPro.Next.UI.Dialogs;

namespace AuswertungPro.Next.UI.Services;

public static class OptionsEditorDialogService
{
    public static DropdownOptionEditorResult Show(IEnumerable<string> items)
    {
        var vm = new OptionsEditorViewModel(items);
        var dialog = new OptionsEditorWindow(vm);
        return new DropdownOptionEditorResult(dialog.ShowDialog() == true, vm.Items.ToArray());
    }
}
