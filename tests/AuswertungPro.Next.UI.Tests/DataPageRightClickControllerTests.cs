using System.IO;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageRightClickControllerTests
{
    [Fact]
    public void Resolve_returns_clear_column_when_clear_mode_has_column()
    {
        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: "Bemerkungen",
            columnDisplayName: "Bemerkungen",
            rowItem: new object());

        Assert.Equal(DataPageRightClickAction.ClearColumn, result.Action);
        Assert.Equal("Bemerkungen", result.FieldName);
        Assert.Equal("Bemerkungen", result.DisplayName);
        Assert.Null(result.RowItem);
    }

    [Fact]
    public void Resolve_prefers_row_selection_when_clear_mode_has_no_column()
    {
        var row = new object();

        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: null,
            columnDisplayName: null,
            rowItem: row);

        Assert.Equal(DataPageRightClickAction.SelectRow, result.Action);
        Assert.Same(row, result.RowItem);
    }

    [Fact]
    public void Resolve_returns_select_row_when_not_in_clear_mode()
    {
        var row = new object();

        var result = DataPageRightClickController.Resolve(
            clearColumnMode: false,
            columnFieldName: "Haltungsname",
            columnDisplayName: "Haltung",
            rowItem: row);

        Assert.Equal(DataPageRightClickAction.SelectRow, result.Action);
        Assert.Same(row, result.RowItem);
    }

    [Fact]
    public void Resolve_returns_none_without_clear_target_or_row()
    {
        var result = DataPageRightClickController.Resolve(
            clearColumnMode: false,
            columnFieldName: null,
            columnDisplayName: null,
            rowItem: null);

        Assert.Equal(DataPageRightClickAction.None, result.Action);
    }

    /// <summary>
    /// Nova-Fixwelle 2b (C3): Eine virtuelle Statusspalte (Nova_*) ist kein Feld. "Spalte
    /// leeren" wuerde dort einen Feldnamen erfinden und in jeden Datensatz schreiben. Der
    /// Rechtsklick auf ihren Kopf tut deshalb nichts.
    /// </summary>
    [Theory]
    [InlineData("Nova_KI")]
    [InlineData("Nova_Pruefung")]
    [InlineData("Nova_Video")]
    [InlineData("Nova_Protokoll")]
    public void Eine_virtuelle_Statusspalte_laesst_sich_nicht_leeren(string feld)
    {
        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: feld,
            columnDisplayName: feld,
            rowItem: null);

        Assert.Equal(DataPageRightClickAction.None, result.Action);
        Assert.Null(result.FieldName);
    }

    /// <summary>Ein Klick in die Zeile waehlt sie weiterhin aus, statt gar nichts zu tun.</summary>
    [Fact]
    public void Auf_einer_virtuellen_Spalte_bleibt_die_Zeilenauswahl()
    {
        var row = new object();

        var result = DataPageRightClickController.Resolve(
            clearColumnMode: true,
            columnFieldName: "Nova_KI",
            columnDisplayName: "KI",
            rowItem: row);

        Assert.Equal(DataPageRightClickAction.SelectRow, result.Action);
        Assert.Same(row, result.RowItem);
    }

    /// <summary>
    /// Nova-Fixwelle 2b, Runde 2: BEIDE Seiten muessen durch diesen Controller laufen. Die
    /// Schachtseite entschied vorher selbst und kannte den Schutz gegen virtuelle Spalten
    /// nicht — „Spalte leeren" auf dem Kopf der Protokollspalte schrieb dort
    /// <c>Nova_Protokoll</c> in jeden Datensatz. Zwei Wege zu derselben Entscheidung heisst,
    /// dass nur einer den Schutz bekommt.
    /// </summary>
    [Theory]
    [InlineData("DataPage.xaml.cs")]
    [InlineData("SchaechtePage.xaml.cs")]
    public void Beide_Seiten_entscheiden_den_Rechtsklick_ueber_diesen_Controller(string datei)
    {
        var quelle = File.ReadAllText(
            TestRepoPaths.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));

        Assert.Contains("DataPageRightClickController.Resolve(", quelle);

        // Geleert wird nur im Zweig des Controllers. Ein eigener Zweig, der die
        // Haekchen-Einstellung selbst auswertet, waere genau der alte Fehler.
        Assert.Contains("case DataPageRightClickAction.ClearColumn", quelle);
        Assert.DoesNotContain("if (ClearColumnModeButton.IsChecked == true)", quelle);
        Assert.DoesNotContain("if (ClearColumnMenuItem.IsChecked == true)", quelle);
    }
}
