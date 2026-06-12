using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Sichert Strategie 4b: IBAK-"Haltungsbildbericht" mit ZWEI Foto-Spalten nebeneinander —
/// "Zustand CODE" und "Entf. METER m" stehen in GETRENNTEN Zeilen (Coverage-Befund
/// 2026-06-12: 15 Sanierungsabnahme-PDFs lieferten 0 Befunde, weil die zeilen-verankerten
/// Muster bei zwei Spalten pro Zeile nichts treffen). Fixture-Struktur stammt aus einem
/// echten Dump (Strassen-/Personennamen ersetzt).
/// </summary>
public sealed class PdfBildberichtZweispaltigTests
{
    // Reale Layout-Struktur: linke Spalte ~Spalte 14, rechte ~Spalte 55-70.
    private const string ZweispaltigerBericht =
        "              Foto     063                                             Foto     064\n" +
        "              Zustand  BDB                                             Zustand  BCA.A.A\n" +
        "              Entf.     0.70 m                                           Entf.     15.60 m\n" +
        "              gegen Flie¦r.                                              gegen Flie¦r.\n" +
        "                              Beginn TV-Untersuchung (Vorgabe)                           Pos: 1; Anschluss mit Formstueck\n" +
        "              Foto     065                                             Foto     066\n" +
        "              Zustand  BCA.A.A                                         Zustand  BCA.A.A\n" +
        "              Entf.     19.90 m                                          Entf.     38.60 m\n" +
        "              gegen Flie¦r.                                              gegen Flie¦r.\n" +
        "              Foto     107                                             Foto     067\n" +
        "              Zustand  BBC.Z                                           Zustand  BCE\n" +
        "              Entf.     45.53 m                                          Entf.     47.90 m\n";

    [Fact]
    public void Zweispaltig_paart_Code_und_Meter_pro_Spalte()
    {
        var entries = PdfProtocolExtractor.ParseZweispaltigerBildbericht(ZweispaltigerBericht);

        // Spaltentreue Paarung: BDB gehoert zu 0.70 (links), BCA.A.A zu 15.60 (rechts) —
        // Naehe-Paarung wuerde die rechte Spalte mit dem linken Meter verkuppeln.
        Assert.Contains(entries, e => e.VsaCode == "BDB" && e.MeterStart == 0.70);
        Assert.Contains(entries, e => e.VsaCode == "BCAAA" && e.MeterStart == 15.60); // Punkte entfernt
        Assert.Contains(entries, e => e.VsaCode == "BCAAA" && e.MeterStart == 19.90);
        Assert.Contains(entries, e => e.VsaCode == "BCAAA" && e.MeterStart == 38.60);
        Assert.Contains(entries, e => e.VsaCode == "BBCZ" && e.MeterStart == 45.53);
        Assert.Contains(entries, e => e.VsaCode == "BCE" && e.MeterStart == 47.90);
        Assert.Equal(6, entries.Count);
    }

    [Fact]
    public void Einspaltiger_Bericht_wird_nicht_angefasst()
    {
        // Schutzgitter: ohne Zeile mit ZWEI Zustand-Treffern liefert 4b nichts —
        // einspaltige Bildberichte laufen unveraendert in Strategie 4 (inkl. Video-Zeit).
        var text =
            "  Zustand  BAF\n" +
            "  Entf.     3.20 m\n" +
            "  Zustand  BCE\n" +
            "  Entf.     47.90 m\n";

        Assert.Empty(PdfProtocolExtractor.ParseZweispaltigerBildbericht(text));
    }

    [Fact]
    public void Unbekannte_Codes_werden_verworfen()
    {
        var text =
            "  Zustand  XYZ                       Zustand  QQQ\n" +
            "  Entf.     1.00 m                     Entf.     2.00 m\n";

        Assert.Empty(PdfProtocolExtractor.ParseZweispaltigerBildbericht(text));
    }
}
