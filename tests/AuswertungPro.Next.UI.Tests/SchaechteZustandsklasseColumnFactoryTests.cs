using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 6: Die Zustandsklasse der Schachtliste verwendet dieselbe Marke wie die
/// Haltungsliste (<see cref="ZustandsklasseChipColumnFactory"/>) — eine Fabrik, ein Aussehen.
/// Die eigene Schachtfabrik ist damit entfallen; ihre Absicherung bleibt hier bestehen.
///
/// Fix-Runde 1 (F1), weiterhin gueltig: Die Zustandsklasse eines Schachts ist ein Handwert und
/// geht in die XTF. Ein Wert, den die Auswahlliste 0 bis 4 nicht kennt (Importwert, Freitext),
/// darf durch blosses Oeffnen und Schliessen der Zelle weder verschwinden noch als Handeingabe
/// gelten. Gemessen: Der Selector setzt <c>SelectedItem</c> auf null, weil er den fremden Wert
/// nicht findet — er schreibt diese Null aber NICHT in die Quelle zurueck.
/// </summary>
public sealed class SchaechteZustandsklasseColumnFactoryTests
{
    [Theory]
    [InlineData("2,4")]
    [InlineData("nicht berechnet")]
    public void Ein_unbekannter_Wert_ueberlebt_das_Oeffnen_und_Schliessen_des_Editors(string wert)
    {
        StaTestRunner.Run(() =>
        {
            var spalte = ZustandsklasseChipColumnFactory.Create("Zustandsklasse", "ZUSTANDSKLASSE");
            var record = new SchachtRecord();
            record.SetFieldValue("Zustandsklasse", wert, FieldSource.Xtf405, userEdited: false);

            var editor = Assert.IsType<ComboBox>(spalte.CellEditingTemplate.LoadContent());
            editor.DataContext = record;
            // Erst das Erzeugen der Eintraege laesst den Selector den Wert suchen.
            editor.Measure(new Size(200, 40));
            editor.Arrange(new Rect(0, 0, 200, 40));
            editor.UpdateLayout();
            WpfBindungsPumpe.Leeren();

            editor.DataContext = null;
            WpfBindungsPumpe.Leeren();

            Assert.Equal(wert, record.GetFieldValue("Zustandsklasse"));
            Assert.False(record.FieldMeta.TryGetValue("Zustandsklasse", out var meta) && meta.UserEdited);
        });
    }

    /// <summary>Ein Schachtdatensatz zeigt dieselbe Marke wie eine Haltung: "Z2", nie eine Ziffer allein.</summary>
    [Fact]
    public void Ein_Schacht_zeigt_die_Zustandsklasse_als_Marke()
    {
        StaTestRunner.Run(() =>
        {
            var spalte = ZustandsklasseChipColumnFactory.Create("Zustandsklasse", "ZUSTANDSKLASSE");
            var wurzel = Assert.IsType<Grid>(spalte.CellTemplate.LoadContent());
            var record = new SchachtRecord();
            record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true);
            wurzel.DataContext = record;
            WpfBindungsPumpe.Leeren();

            var gefuellt = wurzel.Children.OfType<Border>().Single();
            Assert.Equal(Visibility.Visible, gefuellt.Visibility);
            Assert.Equal("Z2", Assert.IsType<TextBlock>(gefuellt.Child).Text);
        });
    }

    /// <summary>
    /// Der Zellen-Commit der Schachtseite liest den bearbeiteten Text aus dem Editierelement.
    /// Bei einer Vorlagenspalte ist das ein <see cref="ContentPresenter"/> — der Leser liefert
    /// dafuer bewusst nichts, und die Seite schreibt deshalb gar nichts. Genau daran haengt,
    /// dass blosses Anklicken der Marke keinen Handstempel setzt (dieselbe Falle wie F2 bei den
    /// Haltungen, dort im eigenen Zustandsklasse-Zweig).
    /// </summary>
    [Fact]
    public void Ein_Zellen_Commit_auf_der_Marke_liefert_keinen_Text_und_stempelt_deshalb_nichts()
    {
        StaTestRunner.Run(() =>
        {
            Assert.False(DataGridEditedTextValueResolver.TryResolve(new ContentPresenter(), out var wert));
            Assert.Equal(string.Empty, wert);
        });
    }
}
