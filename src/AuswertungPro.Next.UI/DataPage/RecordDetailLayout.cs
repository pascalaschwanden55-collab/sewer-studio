namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Eine Spalte der Detailansicht: ihr Titel und die Feldnamen in Anzeigereihenfolge.
/// </summary>
public sealed record RecordDetailLayoutColumn(string Title, IReadOnlyList<string> Fields);

/// <summary>
/// Persoenliche Gestaltung der Detailansicht einer Datensatzart: welche Spalten in
/// welcher Reihenfolge, welche Felder in welcher Spalte, welche Felder ausgeblendet.
///
/// Betrifft ausschliesslich die Anzeige. Die fachliche Feldreihenfolge
/// (<c>FieldCatalog.ColumnOrder</c>) bleibt unangetastet — an ihr haengen CSV-/Excel-Export
/// und der Import-Merge. Ein ausgeblendetes Feld behaelt seinen Wert und geht in jeden
/// Export weiterhin mit.
/// </summary>
public sealed record RecordDetailLayout(
    IReadOnlyList<RecordDetailLayoutColumn> Columns,
    IReadOnlyList<string> HiddenFields)
{
    public static RecordDetailLayout Empty { get; } =
        new(Array.Empty<RecordDetailLayoutColumn>(), Array.Empty<string>());

    /// <summary>
    /// Nichts gespeichert: dann gilt genau das heutige Verhalten aus dem Builder.
    /// Das ist die Rueckfallebene, nicht ein Layout ohne Inhalt.
    /// </summary>
    public bool IsEmpty => Columns.Count == 0 && HiddenFields.Count == 0;
}
