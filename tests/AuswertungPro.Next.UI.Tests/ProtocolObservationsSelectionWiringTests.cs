using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProtocolObservationsSelectionWiringTests
{
    [Fact]
    public void Zeilenauswahl_oeffnet_den_Bearbeitungsdialog_nicht_mehr()
    {
        var (xaml, code) = LiesQuellen();

        // Frueher hing das Oeffnen an SelectionChanged. Das feuerte auch bei jeder
        // Pfeiltaste, die Liste war damit nicht mit der Tastatur begehbar.
        Assert.DoesNotContain("SelectionChanged=\"EntriesGrid_SelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("private void EntriesGrid_SelectionChanged", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Doppelklick_und_Enter_oeffnen_den_Bearbeitungsdialog()
    {
        var (xaml, code) = LiesQuellen();

        Assert.Contains("MouseDoubleClick=\"EntriesGrid_MouseDoubleClick\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyDown=\"EntriesGrid_PreviewKeyDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private void EntriesGrid_MouseDoubleClick", code, StringComparison.Ordinal);
        Assert.Contains("private void EntriesGrid_PreviewKeyDown", code, StringComparison.Ordinal);
        Assert.Contains("ProtocolObservationsEditTriggerPolicy.OpensEditor", code, StringComparison.Ordinal);
        Assert.Contains("ProtocolObservationsEditTriggerPolicy.CanOpenEditor", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Doppelklick_wertet_nur_echte_Datenzeilen()
    {
        var rumpf = LiesMethodenRumpf("EntriesGrid_MouseDoubleClick");

        // Ein Doppelklick auf die Spaltenueberschrift darf nichts oeffnen.
        // VisualTreeSafe ist Pflicht: VisualTreeHelper.GetParent stuerzt auf Text-Runs ab.
        Assert.Contains("VisualTreeSafe.FindAncestor<DataGridRow>", rumpf, StringComparison.Ordinal);
        Assert.DoesNotContain("VisualTreeHelper.GetParent", rumpf, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Aenderungsspur_der_Bearbeitung_bleibt_erhalten()
    {
        // Diese vier Schritte hingen frueher am SelectionChanged-Handler und muessen
        // den Umbau ueberleben, sonst geht eine Bearbeitung verloren. Geprueft wird
        // ausdruecklich NUR der Rumpf der Oeffnen-Methode: dieselben Aufrufe stehen
        // auch in anderen Methoden der Datei und wuerden einen Verlust hier verdecken.
        var rumpf = LiesMethodenRumpf("OpenSelectedEntryForEdit");

        Assert.Contains("Kind = ProtocolChangeKind.Edit", rumpf, StringComparison.Ordinal);
        Assert.Contains("ResortActiveEntries(entry)", rumpf, StringComparison.Ordinal);
        Assert.Contains("MarkDirty()", rumpf, StringComparison.Ordinal);
        Assert.Contains("RefreshRevisionHeader()", rumpf, StringComparison.Ordinal);
    }

    /// <summary>Schneidet genau eine Methode aus der Code-Behind-Datei heraus.</summary>
    private static string LiesMethodenRumpf(string methodenName)
    {
        var (_, code) = LiesQuellen();
        var start = code.IndexOf("private void " + methodenName + "(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Methode {methodenName} fehlt in ProtocolObservationsWindow.xaml.cs.");

        var ende = code.IndexOf("    private ", start + 1, StringComparison.Ordinal);
        return ende < 0 ? code[start..] : code[start..ende];
    }

    private static (string Xaml, string Code) LiesQuellen()
    {
        var views = Path.Combine(FindRepositoryRoot(), "src", "AuswertungPro.Next.UI", "Views");
        return (
            File.ReadAllText(Path.Combine(views, "ProtocolObservationsWindow.xaml")),
            File.ReadAllText(Path.Combine(views, "ProtocolObservationsWindow.xaml.cs")));
    }
}
