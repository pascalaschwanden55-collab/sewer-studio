using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportFortschrittTextTests
{
    [Fact]
    public void Schachtphase_Dateiname_und_Zaehler_bleiben_getrennt()
    {
        var p = new ImportProgress(ImportFortschrittText.Phase(6, "Schachtprotokolle"), 3, 12,
            "Prüfen", @"C:\Kunden\Geheim\Schacht 42.pdf");
        Assert.Equal("Schritt 6 von 7 · Schachtprotokolle", p.Phase);
        Assert.Equal("Datei: Schacht 42.pdf", ImportFortschrittText.Datei(p));
        Assert.Equal("3 von 12", ImportFortschrittText.Zaehler(p));
        Assert.Equal(25, ImportFortschrittText.Prozent(p));
        Assert.Equal("Schachtprotokolle: noch ca. 12 Minuten",
            ImportFortschrittText.Restzeit(p.Phase, TimeSpan.FromMinutes(11.5)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 5)]
    [InlineData(6, 5)]
    public void Unbekannte_oder_ungueltige_Zahlen_erfinden_keinen_Fortschritt(int aktuell, int gesamt)
    {
        var p = new ImportProgress("Archivieren", aktuell, gesamt, "Dateien suchen …");
        Assert.False(ImportFortschrittText.IstBestimmt(p));
        Assert.Equal("", ImportFortschrittText.Zaehler(p));
        Assert.Equal(0, ImportFortschrittText.Prozent(p));
        Assert.Equal("Dateien suchen …", ImportFortschrittText.Datei(p));
    }

    [Fact]
    public void Medien_benennen_die_tatsaechlich_gezaehlte_Haltung()
    {
        var p = new ImportProgress(ImportFortschrittText.Phase(4, "Medien"), 2, 5, "", "100-200");
        Assert.Equal("Haltung: 100-200", ImportFortschrittText.Datei(p));
        Assert.Equal("", ImportFortschrittText.Restzeit(p.Phase, null));
    }
}
