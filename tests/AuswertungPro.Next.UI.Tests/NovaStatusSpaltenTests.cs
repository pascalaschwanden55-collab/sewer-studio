using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 3: Die vier Statusspalten sind virtuell. Sie duerfen weder als Feld
/// gespeichert noch aus einem gespeicherten Layout heraus versteckt oder verschoben werden —
/// ihre Sichtbarkeit steuert allein die Spaltenansicht.
/// </summary>
public sealed class NovaStatusSpaltenTests
{
    [Fact]
    public void Nur_die_vier_Nova_Schluessel_gelten_als_virtuell()
    {
        Assert.Equal(new[] { "Nova_KI", "Nova_Pruefung", "Nova_Video", "Nova_Protokoll" }, NovaStatusSpalten.Alle);
        Assert.All(NovaStatusSpalten.Alle, s => Assert.True(NovaStatusSpalten.IstVirtuell(s)));

        Assert.False(NovaStatusSpalten.IstVirtuell(FieldKeys.ConditionClass));
        Assert.False(NovaStatusSpalten.IstVirtuell(FieldKeys.Link));
        Assert.False(NovaStatusSpalten.IstVirtuell(null));
        Assert.False(NovaStatusSpalten.IstVirtuell(""));
    }

    [Fact]
    public void Kein_Nova_Schluessel_ist_ein_echtes_Feld()
    {
        var felder = new HashSet<string>(FieldCatalog.ColumnOrder, StringComparer.Ordinal);
        Assert.All(NovaStatusSpalten.Alle, s => Assert.DoesNotContain(s, felder));
    }

    [Fact]
    public void Das_gespeicherte_Spaltenlayout_kennt_keine_virtuelle_Spalte()
    {
        StaTestRunner.Run(() =>
        {
            var controller = new DataGridColumnLayoutController();
            var layout = controller.Capture(Spalten());

            Assert.Contains(layout.Columns, c => c.FieldName == FieldKeys.HoldingName);
            Assert.All(NovaStatusSpalten.Alle, s => Assert.DoesNotContain(layout.Columns, c => c.FieldName == s));
        });
    }

    [Fact]
    public void Ein_alter_Layout_Eintrag_versteckt_keine_virtuelle_Spalte()
    {
        StaTestRunner.Run(() =>
        {
            var spalten = Spalten();
            var controller = new DataGridColumnLayoutController();

            // So koennte ein Layout aussehen, das jemand von Hand oder aus einer aelteren
            // Fassung eingespielt hat: Es nennt eine virtuelle Spalte als vermeintliches Feld.
            controller.Restore(spalten, new DataPageLayoutSettings
            {
                Columns =
                [
                    new DataPageColumnLayout { FieldName = NovaStatusSpalten.Ki, IsVisible = false, DisplayIndex = 0 }
                ]
            });

            var ki = spalten.Single(s => (string?)s.GetValue(FrameworkElement.TagProperty) == NovaStatusSpalten.Ki);
            Assert.Equal(Visibility.Visible, ki.Visibility);
        });
    }

    /// <summary>
    /// Fix-Runde 1 (F3): Auch die Reihenfolge darf ein handgesetzter Nova-Eintrag nicht steuern.
    /// Die Statusspalten bleiben hinter den echten Feldern, egal welchen DisplayIndex das
    /// gespeicherte Layout fuer sie nennt.
    /// </summary>
    [Fact]
    public void Ein_alter_Layout_Eintrag_verschiebt_keine_virtuelle_Spalte()
    {
        StaTestRunner.Run(() =>
        {
            var spalten = Spalten();
            new DataGridColumnLayoutController().Restore(spalten, new DataPageLayoutSettings
            {
                Columns =
                [
                    // Der Eintrag wollte die KI-Spalte ganz nach vorn ziehen.
                    new DataPageColumnLayout { FieldName = NovaStatusSpalten.Ki, IsVisible = true, DisplayIndex = 0 },
                    new DataPageColumnLayout { FieldName = FieldKeys.HoldingName, IsVisible = true, DisplayIndex = 1 }
                ]
            });

            var reihenfolge = spalten
                .OrderBy(s => s.DisplayIndex)
                .Select(s => (string?)s.GetValue(FrameworkElement.TagProperty))
                .ToArray();

            Assert.Equal(FieldKeys.HoldingName, reihenfolge[0]);
            Assert.Equal(NovaStatusSpalten.Alle.ToArray(), reihenfolge.Skip(1).Select(n => n ?? "").ToArray());
        });
    }

    private static List<DataGridColumn> Spalten()
    {
        var name = new DataGridTextColumn();
        name.SetValue(FrameworkElement.TagProperty, FieldKeys.HoldingName);

        var spalten = new List<DataGridColumn> { name };
        foreach (var (schluessel, spalte) in new (string, DataGridColumn)[]
                 {
                     (NovaStatusSpalten.Ki, HaltungStatusColumnFactory.Ki("KI")),
                     (NovaStatusSpalten.Pruefung, HaltungStatusColumnFactory.Pruefung("PRÜFUNG")),
                     (NovaStatusSpalten.Video, HaltungStatusColumnFactory.Video("VIDEO")),
                     (NovaStatusSpalten.Protokoll, HaltungStatusColumnFactory.Protokoll("PROTOKOLL"))
                 })
        {
            spalte.SetValue(FrameworkElement.TagProperty, schluessel);
            spalten.Add(spalte);
        }

        return spalten;
    }
}
