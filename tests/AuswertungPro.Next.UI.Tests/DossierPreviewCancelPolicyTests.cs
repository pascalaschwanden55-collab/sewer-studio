using System.IO;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPreviewCancelPolicyTests
{
    [Fact]
    public void Nach_einer_Eingabe_wird_vor_dem_Verwerfen_gefragt()
    {
        // Esc oder das Fenster-X warfen die geschriebenen Dossiertexte bisher
        // ohne Rueckfrage weg.
        Assert.True(DossierPreviewCancelPolicy.NeedsDiscardConfirmation(
            hasChanges: true, isAccepting: false));
    }

    [Fact]
    public void Uebernehmen_fragt_nicht_nach()
    {
        // Uebernehmen setzt DialogResult und loest damit dasselbe Schliessen aus.
        // Ohne diese Unterscheidung kaeme die Verwerfen-Frage beim Speichern.
        Assert.False(DossierPreviewCancelPolicy.NeedsDiscardConfirmation(
            hasChanges: true, isAccepting: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ohne_Eingabe_schliesst_die_Vorschau_ohne_Rueckfrage(bool isAccepting)
    {
        Assert.False(DossierPreviewCancelPolicy.NeedsDiscardConfirmation(
            hasChanges: false, isAccepting));
    }

    [Fact]
    public void Das_Fenster_meldet_jede_Eingabe_und_fragt_beim_Schliessen()
    {
        var code = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "AuswertungPro.Next.UI", "Views", "Windows",
            "DossierPreviewWindow.xaml.cs"));

        // ZeichneBlatt ist der einzige Weg, auf dem eine Eingabe ins Dossier laeuft.
        var zeichneBlatt = Rumpf(code, "private void ZeichneBlatt()");
        Assert.Contains("_hatAenderungen = true;", zeichneBlatt, StringComparison.Ordinal);

        var onClosing = Rumpf(code, "protected override void OnClosing(");
        Assert.Contains("DossierPreviewCancelPolicy.NeedsDiscardConfirmation", onClosing, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", onClosing, StringComparison.Ordinal);
    }

    private static string Rumpf(string code, string signatur)
    {
        var start = code.IndexOf(signatur, StringComparison.Ordinal);
        Assert.True(start >= 0, $"'{signatur}' fehlt in DossierPreviewWindow.xaml.cs.");
        var ende = code.IndexOf("    private ", start + 1, StringComparison.Ordinal);
        var ende2 = code.IndexOf("    protected ", start + 1, StringComparison.Ordinal);
        if (ende2 >= 0 && (ende < 0 || ende2 < ende)) ende = ende2;
        return ende < 0 ? code[start..] : code[start..ende];
    }
}
