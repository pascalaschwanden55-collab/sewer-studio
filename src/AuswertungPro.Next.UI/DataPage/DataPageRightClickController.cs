namespace AuswertungPro.Next.UI.DataPage;

public enum DataPageRightClickAction
{
    None,
    ClearColumn,
    SelectRow
}

public sealed record DataPageRightClickResult(
    DataPageRightClickAction Action,
    string? FieldName,
    string? DisplayName,
    object? RowItem);

public static class DataPageRightClickController
{
    public static DataPageRightClickResult Resolve(
        bool clearColumnMode,
        string? columnFieldName,
        string? columnDisplayName,
        object? rowItem)
    {
        // Nova-Fixwelle 2b (C3): Eine virtuelle Statusspalte (Nova_*) ist kein Feld. "Spalte
        // leeren" wuerde dort einen Feldnamen erfinden und ihn in jeden Datensatz schreiben.
        // Ein Rechtsklick auf ihren Kopf tut deshalb nichts; die Zeilenauswahl bleibt.
        if (clearColumnMode
            && !string.IsNullOrWhiteSpace(columnFieldName)
            && !NovaStatusSpalten.IstVirtuell(columnFieldName))
        {
            return new DataPageRightClickResult(
                DataPageRightClickAction.ClearColumn,
                columnFieldName,
                string.IsNullOrWhiteSpace(columnDisplayName) ? columnFieldName : columnDisplayName,
                RowItem: null);
        }

        if (rowItem is not null)
            return new DataPageRightClickResult(DataPageRightClickAction.SelectRow, null, null, rowItem);

        return new DataPageRightClickResult(DataPageRightClickAction.None, null, null, null);
    }
}
