using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportRestzeitSchaetzerTests
{
    [Fact]
    public void Erst_drei_erledigte_Dateien_liefern_eine_Restzeit()
    {
        var regel = new ImportRestzeitSchaetzer();
        Assert.Null(Melde(regel, "Schacht", 0, 12, 0));
        Assert.Null(Melde(regel, "Schacht", 2, 12, 120));
        Assert.Equal(TimeSpan.FromMinutes(9), Melde(regel, "Schacht", 3, 12, 180));
        // Meldung beim Beginn der naechsten Datei ist kein weiterer Abschluss.
        Assert.Equal(TimeSpan.FromMinutes(9), Melde(regel, "Schacht", 3, 12, 190));
        Assert.Null(Melde(regel, "Schacht", 12, 12, 720));
    }

    [Fact]
    public void Schrittwechsel_verwirft_die_vorherige_Dauer()
    {
        var regel = new ImportRestzeitSchaetzer();
        Melde(regel, "Medien", 0, 12, 0);
        Melde(regel, "Medien", 3, 12, 900);
        Assert.Null(Melde(regel, "Schacht", 0, 0, 1000));
        Assert.Equal(TimeSpan.FromMinutes(3), Melde(regel, "Schacht", 3, 12, 1060));
        Assert.Null(Melde(regel, "Abschluss", 0, 0, 1100));
    }

    [Theory]
    [InlineData(1, 12)]
    [InlineData(4, 20)]
    public void Ruecklauf_oder_neue_Gesamtzahl_startet_neue_Messung(int aktuell, int gesamt)
    {
        var regel = new ImportRestzeitSchaetzer();
        Melde(regel, "Schacht", 0, 12, 0);
        Melde(regel, "Schacht", 3, 12, 90);
        Assert.Null(Melde(regel, "Schacht", aktuell, gesamt, 100));
    }

    [Fact]
    public void Einstieg_mitten_im_Schritt_erfindet_keine_vergangene_Dauer()
    {
        var regel = new ImportRestzeitSchaetzer();
        Assert.Null(Melde(regel, "Schacht", 4, 12, 100));
        Assert.Null(Melde(regel, "Schacht", 6, 12, 160));
        Assert.Equal(TimeSpan.FromSeconds(150), Melde(regel, "Schacht", 7, 12, 190));
    }

    private static TimeSpan? Melde(ImportRestzeitSchaetzer regel, string phase, int aktuell, int gesamt, int sekunden)
        => regel.Aktualisiere(new ImportProgress(phase, aktuell, gesamt, ""), TimeSpan.FromSeconds(sekunden));
}
