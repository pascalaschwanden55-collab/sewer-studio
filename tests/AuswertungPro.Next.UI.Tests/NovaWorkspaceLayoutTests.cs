using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Die eine gemeinsame Layoutregel der Nova-Arbeitsflaechen (Haltungen und Schaechte), B4 Deepscan 02.10.2026.
/// Laeuft im isolierten Kindprozess (WPF-Regel).
/// </summary>
public sealed class NovaWorkspaceLayoutTests
{
    [Fact]
    public async Task Layoutregeln_funktionieren_isoliert()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            GetType().FullName + "." + nameof(Kindprozess_prueft_Layoutregeln), TimeSpan.FromSeconds(90));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0, result.DescribeFailure());
        Assert.True(result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess_prueft_Layoutregeln()
    {
        StaTestRunner.Run(() =>
        {
            var f = NeueFlaechen();
            var hoehenaufrufe = 0;

            // Zugeklappt: Kopfzeile bleibt, Zeile Auto, Trennlinie weg.
            NovaWorkspaceLayout.WendeSchubladeAn(f, istOffen: false, () => hoehenaufrufe++);
            Check(f.DrawerSplitter.Visibility == Visibility.Collapsed, "zu: Trennlinie weg");
            Check(f.DrawerSplitterRow.Height.Value == 0, "zu: Trennzeile 0");
            Check(f.DrawerRow.MinHeight == 0 && f.DrawerRow.Height.IsAuto, "zu: Zeile Auto");
            Check(hoehenaufrufe == 0, "zu: keine Hoehenberechnung");

            // Aufgeklappt aus Auto: feste Mindesthoehe, danach Hoehenberechnung.
            NovaWorkspaceLayout.WendeSchubladeAn(f, istOffen: true, () => hoehenaufrufe++);
            Check(f.DrawerSplitter.Visibility == Visibility.Visible, "auf: Trennlinie da");
            Check(f.DrawerSplitterRow.Height.Value == DataPageWorkspaceLayoutPolicy.SplitterHoehe, "auf: Trennzeile");
            Check(f.DrawerRow.MinHeight == DataPageWorkspaceLayoutPolicy.MinDrawer, "auf: Mindesthoehe");
            Check(!f.DrawerRow.Height.IsAuto && f.DrawerRow.Height.Value == DataPageWorkspaceLayoutPolicy.MinDrawer, "auf: feste Hoehe");
            Check(hoehenaufrufe == 1, "auf: Hoehenberechnung genau einmal");

            // Nur Uebersicht: Schublade verschwindet ganz, Seitenspalte mit gespeicherter (geklemmter) Breite.
            var schubladenaufrufe = 0;
            NovaWorkspaceLayout.SetzeSichtbar(f, uebersicht: true, eingabefelder: false, () => 9999d, () => schubladenaufrufe++);
            Check(f.Uebersicht.Visibility == Visibility.Visible && f.SideSplitter.Visibility == Visibility.Visible, "nur Uebersicht: sichtbar");
            Check(f.FelderDrawer.Visibility == Visibility.Collapsed, "nur Uebersicht: Drawer weg");
            Check(f.SideCol.Width.Value == NovaWorkspaceLayout.SideColMax, "Breite geklemmt auf Maximum");
            Check(f.SideCol.MinWidth == NovaWorkspaceLayout.SideColMin && f.SideCol.MaxWidth == NovaWorkspaceLayout.SideColMax, "Spaltengrenzen");
            Check(f.DrawerRow.Height.Value == 0 && f.DrawerRow.MinHeight == 0 && f.DrawerSplitterRow.Height.Value == 0, "Zeilen 0");
            Check(schubladenaufrufe == 0, "Schublade nicht angewendet");

            // Ohne gespeicherte Breite: Standard. Beide sichtbar: Schublade wird angewendet.
            NovaWorkspaceLayout.SetzeSichtbar(f, uebersicht: true, eingabefelder: true, () => null, () => schubladenaufrufe++);
            Check(f.SideCol.Width.Value == NovaWorkspaceLayout.SideColStandard, "Standardbreite");
            Check(f.FelderDrawer.Visibility == Visibility.Visible && schubladenaufrufe == 1, "Drawer sichtbar und angewendet");

            // Nur Eingabefelder: Seitenspalte und Trennspalte 0.
            NovaWorkspaceLayout.SetzeSichtbar(f, uebersicht: false, eingabefelder: true, () => 400d, () => schubladenaufrufe++);
            Check(f.Uebersicht.Visibility == Visibility.Collapsed && f.SideSplitter.Visibility == Visibility.Collapsed, "Uebersicht weg");
            Check(f.SideCol.Width.Value == 0 && f.SideCol.MinWidth == 0 && f.SideSplitterCol.Width.Value == 0, "Seitenspalte 0");

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
        });
    }

    private static NovaWorkspaceLayout.Flaechen NeueFlaechen() => new(
        new Border(), new GridSplitter(), new Border(),
        new ColumnDefinition(), new ColumnDefinition(),
        new GridSplitter(), new RowDefinition(), new RowDefinition());

    private static void Check(bool bedingung, string was)
    {
        if (!bedingung)
            throw new InvalidOperationException("Layoutregel verletzt: " + was);
    }
}
