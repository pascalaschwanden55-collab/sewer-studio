using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataPageColumnFactory
{
    public static DataGridColumn Create(
        string fieldName,
        string header,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged)
    {
        if (GridDropdownFieldPolicy.TryResolve(fieldName, out var comboSpec))
        {
            return comboSpec.Managed
                ? CreateManagedComboColumn(fieldName, header, comboSpec, lostKeyboardFocus, selectionChanged)
                : CreateSimpleComboColumn(fieldName, header, comboSpec.ItemsSourcePath, lostKeyboardFocus, selectionChanged);
        }

        if (fieldName == "Empfohlene_Sanierungsmassnahmen")
            return DataGridWrappingTextColumnFactory.Create(fieldName, header);

        if (fieldName == "Kosten")
            return DataGridCostColumnFactory.Create(fieldName, header);

        var updateSourceTrigger = fieldName == "Haltungsname"
            ? UpdateSourceTrigger.Explicit
            : UpdateSourceTrigger.LostFocus;
        return DataGridStandardTextColumnFactory.Create(fieldName, header, updateSourceTrigger);
    }

    private static DataGridTemplateColumn CreateManagedComboColumn(
        string fieldName,
        string header,
        GridDropdownFieldSpec spec,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged)
        => DataGridComboColumnFactory.Create(
            fieldName,
            header,
            spec.ItemsSourcePath,
            tag: fieldName,
            lostKeyboardFocus,
            selectionChanged,
            spec.AllowFreeText,
            bindIsProjectReady: true,
            menuCommands: new DataGridComboColumnMenuCommands(
                spec.EditCommand,
                spec.PreviewCommand,
                spec.ResetCommand,
                spec.RemoveCommand,
                spec.AddCommand));

    private static DataGridTemplateColumn CreateSimpleComboColumn(
        string fieldName,
        string header,
        string itemsSourcePath,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged)
        => DataGridComboColumnFactory.Create(
            fieldName,
            header,
            itemsSourcePath,
            tag: fieldName,
            lostKeyboardFocus,
            selectionChanged,
            allowFreeText: true,
            bindIsProjectReady: true);
}
