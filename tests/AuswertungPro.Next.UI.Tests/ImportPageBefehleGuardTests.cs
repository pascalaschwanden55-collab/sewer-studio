using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter zur Neuordnung der Importseite (28.09.2026): Die Seite wurde nur umgestellt
/// (Normalfall oben, Sonderfaelle darunter). Jeder bisherige Befehl muss weiterhin direkt
/// auf der Seite gebunden sein - ein Importweg darf beim Umbau nicht still verschwinden.
/// </summary>
public sealed class ImportPageBefehleGuardTests
{
    private static string Xaml() => File.ReadAllText(
        RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ImportPage.xaml"));

    [Theory]
    [InlineData("ImportKanalProjektCommand")]
    [InlineData("ImportPdfCommand")]
    [InlineData("ImportXtfCommand")]
    [InlineData("ImportWinCanCommand")]
    [InlineData("ImportIbakCommand")]
    [InlineData("ImportKinsCommand")]
    [InlineData("ImportSchachtProCommand")]
    [InlineData("ImportSchachtProQrCommand")]
    [InlineData("ImportSchachtPdfsFolderCommand")]
    [InlineData("AssignPhotosFromFolderCommand")]
    [InlineData("ProtokollNeuGenerierenCommand")]
    [InlineData("MakeProjectPortableCommand")]
    [InlineData("ExportImportSummaryCommand")]
    [InlineData("OpenLastReportCommand")]
    [InlineData("OpenReportFolderCommand")]
    [InlineData("ReloadCatalogCommand")]
    [InlineData("CancelImportCommand")]
    public void Jeder_Importbefehl_bleibt_auf_der_Seite_gebunden(string befehl)
    {
        Assert.Contains($"Command=\"{{Binding {befehl}}}\"", Xaml(), StringComparison.Ordinal);
    }

    [Fact]
    public void Importquellen_liegen_nicht_mehr_in_einem_versteckten_Aufklappmenue()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("<ContextMenu>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownButton_Click", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Nacharbeit_heisst_zuordnen_nicht_verteilen()
    {
        // «Verteilen» gibt es auf der Export-Seite; hier werden vorhandene PDFs nur zugeordnet.
        var xaml = Xaml();
        Assert.Contains("Protokolle aus einem Ordner zuordnen", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"Protokolle verteilen\"", xaml, StringComparison.Ordinal);
    }
}
