using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Welches pdftotext ein Rechner findet, entscheidet ueber die Richtigkeit der
/// Schachtanschluesse. Gemessen am 19.09.2026 an 264 SchachtPro-Protokollen:
/// Poppler 25.07 liest 704 von 704 Anschluessen, Xpdf 4.00 nur 627 und ordnet
/// den gelesenen teilweise fremde Tiefen zu. Die Versionsausgaben stammen
/// woertlich von beiden Programmen dieses Rechners.
/// </summary>
public sealed class PdfLeserEignungTests
{
    private const string PopplerAusgabe =
        "pdftotext version 25.07.0\n"
        + "Copyright 2005-2025 The Poppler Developers - http://poppler.freedesktop.org\n"
        + "Copyright 1996-2011, 2022 Glyph & Cog, LLC";

    private const string XpdfAusgabe =
        "pdftotext version 4.00\n"
        + "Copyright 1996-2017 Glyph & Cog, LLC";

    [Fact]
    public void Poppler_2507_ist_geeignet()
    {
        var urteil = PdfLeserEignung.Beurteile(PopplerAusgabe);

        Assert.True(urteil.Geeignet);
        Assert.Equal("Poppler", urteil.Hersteller);
        Assert.Equal(25, urteil.Version);
    }

    [Fact]
    public void Xpdf_ist_ungeeignet_obwohl_die_Poppler_Ausgabe_denselben_Copyright_Hinweis_traegt()
    {
        // Poppler stammt von Xpdf ab und nennt ebenfalls "Glyph & Cog".
        // Nur das Fehlen von "Poppler" macht die Ausgabe zu Xpdf.
        Assert.Contains("Glyph & Cog", PopplerAusgabe, StringComparison.Ordinal);

        var urteil = PdfLeserEignung.Beurteile(XpdfAusgabe);

        Assert.False(urteil.Geeignet);
        Assert.Equal("Xpdf", urteil.Hersteller);
        Assert.Equal(4, urteil.Version);
        Assert.Contains("Anschlusstabelle", urteil.Grund, StringComparison.Ordinal);
    }

    [Fact]
    public void Zu_alte_Poppler_Fassung_wird_nicht_verwendet()
    {
        var urteil = PdfLeserEignung.Beurteile(
            "pdftotext version 0.68.0\nCopyright 2005-2018 The Poppler Developers");

        Assert.False(urteil.Geeignet);
        Assert.Equal("Poppler", urteil.Hersteller);
        Assert.Equal(0, urteil.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("irgendein anderes Programm 9.9")]
    public void Ohne_erkennbaren_Hersteller_wird_der_eingebaute_Leser_verwendet(string? ausgabe)
    {
        var urteil = PdfLeserEignung.Beurteile(ausgabe);

        Assert.False(urteil.Geeignet);
        Assert.Contains("eingebaute", urteil.Grund, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Die_Untergrenze_gilt_genau_ab_der_gepruefen_Fassung()
    {
        var knappDarunter = PdfLeserEignung.Beurteile(
            $"pdftotext version {PdfLeserEignung.KleinstePopplerVersion - 1}.12.0\nThe Poppler Developers");
        var genauAufDerGrenze = PdfLeserEignung.Beurteile(
            $"pdftotext version {PdfLeserEignung.KleinstePopplerVersion}.0.0\nThe Poppler Developers");

        Assert.False(knappDarunter.Geeignet);
        Assert.True(genauAufDerGrenze.Geeignet);
    }
}
