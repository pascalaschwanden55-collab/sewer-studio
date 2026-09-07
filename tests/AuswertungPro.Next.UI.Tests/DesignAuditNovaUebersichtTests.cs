using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaUebersichtTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ProjektUebersichtPage.xaml"));

    [Fact]
    public void Uebersicht_hat_Hero_Vorabdurchlauf_vier_Kennzahlen_Ring_und_Schaeden()
    {
        var xaml = Xaml();
        foreach (var text in new[] { "Nächste Haltung prüfen", "Haltungen öffnen", "KI-Vorabdurchlauf", "Haltungen", "Schächte", "Dringend (Z0/Z1)", "Sanierungskosten", "Zustand Haltungen", "Häufigste Schäden", "Projekte", "Sanierungsverfahren", "Stammdaten" })
            Assert.Contains(text, xaml);
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("#", xaml.Replace("&#x", ""));
    }

    [Fact]
    public void Im_Projekt_zeigt_Uebersicht_die_neue_Seite_und_der_Start_die_Projektliste()
    {
        var shell = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        Assert.Contains("new Pages.ProjektUebersichtPageViewModel(this, _sp)", shell);
        Assert.Contains("new Pages.OverviewPageViewModel(this, _sp)", shell);
    }
}
