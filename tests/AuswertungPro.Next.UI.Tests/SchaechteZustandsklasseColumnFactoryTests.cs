using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Fix-Runde 1 (F1) zu Nova-Etappe 2b: Dieselbe Absicherung wie fuer die Haltungsmarke, hier am
/// bestehenden Muster der Schachtliste. Die Zustandsklasse eines Schachts ist ein Handwert und
/// geht in die XTF — ein Wert, den die Auswahlliste 0 bis 4 nicht kennt (Importwert, Freitext),
/// darf durch blosses Oeffnen und Schliessen der Zelle nicht verschwinden.
///
/// Gemessen: Der Selector setzt <c>SelectedItem</c> auf null, weil er den fremden Wert in seinen
/// Eintraegen nicht findet — er schreibt diese Null aber NICHT in die Quelle zurueck. Der Wert
/// bleibt also stehen. Dieser Test haelt genau das fest, damit die TwoWay-Bindung nicht
/// unbemerkt zu einem Datenverlust wird.
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
            var spalte = SchaechteZustandsklasseColumnFactory.Create("Zustandsklasse", "ZUSTANDSKLASSE");
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
}
