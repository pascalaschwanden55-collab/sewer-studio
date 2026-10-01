using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataPageColumnFactory
{
    public static DataGridColumn Create(
        string fieldName,
        string header,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged,
        bool novaLayout = true)
    {
        // Nova-Etappe 2b: Der Tabellenkopf schreibt gross (Kapitaelchen greifen mit der
        // Programmschrift nicht). Die Umwandlung passiert hier statt im Kopf-Template, weil
        // Theme.xaml/ThemeLight.xaml von PageTitleUnderlineTests roh per XamlReader.Load
        // geladen werden und dort keinen eigenen Konverter-Typ referenzieren duerfen (siehe
        // GrossbuchstabenConverter-Doku).
        header = GrossbuchstabenConverter.Anwenden(header) ?? header;

        if (GridDropdownFieldPolicy.TryResolve(fieldName, out var comboSpec))
        {
            return comboSpec.Managed
                ? CreateManagedComboColumn(fieldName, header, comboSpec, lostKeyboardFocus, selectionChanged)
                : CreateSimpleComboColumn(fieldName, header, comboSpec.ItemsSourcePath, lostKeyboardFocus, selectionChanged);
        }

        // Nova-Etappe 2b, Fix-Runde 1 (F4): Die Drei-Zeilen-Grenze gilt nur im Nova-Layout;
        // die alte Haltungsansicht bleibt genau wie bisher.
        if (DataPageColumnStyleRules.IstUmbruchspalte(fieldName))
            return DataGridWrappingTextColumnFactory.Create(fieldName, header, novaLayout);

        if (fieldName == "Kosten")
            return DataGridCostColumnFactory.Create(fieldName, header);

        var updateSourceTrigger = fieldName == "Haltungsname"
            ? UpdateSourceTrigger.Explicit
            : UpdateSourceTrigger.LostFocus;
        return DataGridStandardTextColumnFactory.Create(fieldName, header, updateSourceTrigger, novaLayout);
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
