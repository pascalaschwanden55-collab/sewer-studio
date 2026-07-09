using System.IO;

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
