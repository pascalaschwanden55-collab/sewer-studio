using System.Globalization;
using System.IO;
using System.Windows;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewPageLayoutTests
{
    [Fact]
    public void Toolbar_enthaelt_keine_globalen_oeffnen_fortsetzen_aktualisieren_buttons()
    {
        var xaml = ReadOverviewXaml();

        Assert.DoesNotContain("Command=\"{Binding OpenCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding ContinueCommand}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Command=\"{Binding RefreshCommand}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Vorschaukopf_hat_pdf_button_und_kompakte_aktionsgruppe()
    {
        var xaml = ReadOverviewXaml();

        Assert.Contains("LastChildFill=\"False\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding PrintPreviewPdfCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"PDF\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Projekt öffnen\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Projektliste_verwendet_kartenlayout_und_symmetrische_aktionen()
    {
        var xaml = ReadOverviewXaml();

        Assert.Contains("Text=\"Projektvorschauen\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"74\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding FolderName, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<UniformGrid Grid.Row=\"3\" Columns=\"2\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Projektliste_bezeichnet_entfernen_eindeutig_und_nicht_als_ausblenden()
    {
        var xaml = ReadOverviewXaml();

        Assert.Contains("Content=\"Entfernen\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Projekt nur aus der Übersicht entfernen", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Ausblenden", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Seitenkopf_zeigt_keinen_speicherstatus_unter_der_ueberschrift()
    {
        var xaml = ReadOverviewXaml();

        Assert.DoesNotContain("Text=\"{Binding ProjectStatus}\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Projektliste_hat_genug_breite_fuer_vorschaukarten()
    {
        var converter = new ProjectListWidthConverter();

        var width = Assert.IsType<GridLength>(converter.Convert(false, typeof(GridLength), null, CultureInfo.InvariantCulture));

        Assert.Equal(360, width.Value);
    }

    private static string ReadOverviewXaml()
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "OverviewPage.xaml"));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "AuswertungPro.sln")))
                return dir;

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
