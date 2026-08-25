using System;
using System.IO;

using AuswertungPro.Next.UI.Views.Rendering;

using Xunit;

using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierPreviewFitTests
{
    [Fact]
    public void Ganze_A4_Seite_passt_sich_an_die_verfuegbare_Hoehe_an()
    {
        var scale = DossierPreviewFitCalculator.Calculate(
            viewportWidth: 760,
            viewportHeight: 730,
            pageWidth: 794,
            pageHeight: 1123,
            surroundingSpace: 60);

        Assert.Equal(670d / 1123d, scale, precision: 3);
    }

    [Fact]
    public void Ungueltige_Masse_liefern_einen_sicheren_Standardwert()
        => Assert.Equal(1d, DossierPreviewFitCalculator.Calculate(0, 730, 794, 1123, 60));

    [Fact]
    public void Vorschaufenster_startet_mit_Ganze_Seite_und_erklaert_den_Knopf()
    {
        var xaml = File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Windows", "DossierPreviewWindow.xaml"));

        Assert.Contains("Content=\"Ganze Seite\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnFitPage\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Loaded=\"OnPreviewLoaded\"", xaml, StringComparison.Ordinal);
    }
}
