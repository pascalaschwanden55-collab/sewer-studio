using System;

using AuswertungPro.Next.Application.Dossiers;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Woran eine Zeile des Inhaltsverzeichnisses erkannt wird.
///
/// Sie sieht wie fester Text aus, ist aber ein Word-Feld: „1.Übersichtsplan
/// Werkleitungen" trägt am Ende ein PAGEREF mit der Seitenzahl. Als
/// bearbeitbarer Text angeboten, wanderte diese Seitenzahl in den Schlüssel —
/// und die eigene Fassung wurde ungültig, sobald sich die Seiten verschoben.
/// </summary>
public sealed class DossierTocStyleTests
{
    [Theory]
    [InlineData("Verzeichnis1")]
    [InlineData("Verzeichnis9")]
    [InlineData("verzeichnis3")]
    [InlineData("TOC1")]
    [InlineData("TOC 2")]
    [InlineData("toc1")]
    public void Verzeichniszeilen_werden_erkannt(string stil)
        => Assert.True(DossierTocStyle.IsEntry(stil));

    [Theory]
    [InlineData("Titel")]          // die Überschrift „Inhaltsverzeichnis" selbst
    [InlineData("berschrift1")]
    [InlineData("Standard")]
    [InlineData("Verzeichnis")]    // ohne Stufe: kein Verzeichniseintrag
    [InlineData("Verzeichnisse")]
    [InlineData("")]
    [InlineData(null)]
    public void Alles_andere_ist_keine_Verzeichniszeile(string? stil)
        => Assert.False(DossierTocStyle.IsEntry(stil));

    [Fact]
    public void Die_Ueberschrift_des_Verzeichnisses_bleibt_fester_Text()
    {
        // Sie ist wirklich fest — Word rechnet nur die Zeilen darunter.
        Assert.False(DossierTocStyle.IsEntry("Titel"));
    }
}
