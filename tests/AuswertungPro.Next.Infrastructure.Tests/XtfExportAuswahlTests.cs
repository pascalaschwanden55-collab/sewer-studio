using AuswertungPro.Next.Application.UseCases.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Exportseite empfiehlt den XTF-Weg nach dem Projekt, nicht fest: Mit einer Importkopie
/// ist "Bestehende Katasterdaten aktualisieren" richtig, ohne Kopie "Neue eigenstaendige XTF".
/// Reine Rechnung ohne Dateizugriff.
/// </summary>
public sealed class XtfExportAuswahlTests
{
    [Fact]
    public void Mit_Importkopie_wird_Aktualisieren_empfohlen_und_das_Original_genannt()
    {
        var auswahl = XtfExportAuswahl.Aus(
        [
            new XtfProjektkopie(@"C:\Projekt\Imports\XTF\Leitungen_Export.xtf", new DateTime(2025, 11, 18, 9, 30, 0))
        ]);

        Assert.Equal(XtfExportWeg.Aktualisieren, auswahl.Empfohlen);
        Assert.Equal("Original: Leitungen_Export.xtf — Importkopie vom 18.11.2025", auswahl.OriginalZeile);
        Assert.Contains("Duplikate", auswahl.NeuHinweis, StringComparison.Ordinal);
    }

    [Fact]
    public void Mehrere_Kopien_nennen_die_neueste_und_zaehlen_die_uebrigen()
    {
        var auswahl = XtfExportAuswahl.Aus(
        [
            new XtfProjektkopie(@"C:\Projekt\Imports\XTF\alt.xtf", new DateTime(2024, 1, 5)),
            new XtfProjektkopie(@"C:\Projekt\Imports\XTF\neu.xtf", new DateTime(2025, 11, 18)),
            new XtfProjektkopie(@"C:\Projekt\Imports\XTF\mittel.xtf", new DateTime(2025, 3, 1))
        ]);

        Assert.Equal(XtfExportWeg.Aktualisieren, auswahl.Empfohlen);
        Assert.Equal("Original: neu.xtf — Importkopie vom 18.11.2025 · + 2 weitere", auswahl.OriginalZeile);
    }

    [Fact]
    public void Ohne_Importkopie_wird_der_Neuexport_empfohlen()
    {
        var auswahl = XtfExportAuswahl.Aus([]);

        Assert.Equal(XtfExportWeg.Neu, auswahl.Empfohlen);
        Assert.Equal("Keine Importkopie im Projekt — beim Start wählst du die Original-XTF.", auswahl.OriginalZeile);
        Assert.Equal("Für Leitungen, die im Kataster noch fehlen.", auswahl.NeuHinweis);
    }
}
