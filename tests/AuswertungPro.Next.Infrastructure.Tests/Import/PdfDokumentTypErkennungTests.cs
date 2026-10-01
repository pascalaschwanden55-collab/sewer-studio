using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class PdfDokumentTypErkennungTests
{
    [Theory]
    [InlineData("SCHACHTPRO Projekt: Test Datum: 13.08.2026Schachtprotokoll Schacht Nr. 10051STAMMDATEN & SKIZZE")]
    [InlineData("Schachtinspektion Schacht 9072")]
    [InlineData("Schachtbericht 1234")]
    public void ErkenneText_SchachtHatEigenenVerteilweg(string text)
        => Assert.Equal(PdfDokumentTyp.Schachtprotokoll, PdfDokumentTypErkennung.ErkenneText(text, "10051.pdf"));

    [Fact]
    public void ErkenneText_GemischtesSammelprotokollBleibtImHaltungsweg()
        => Assert.Equal(PdfDokumentTyp.TvProtokoll,
            PdfDokumentTypErkennung.ErkenneText("Schachtprotokoll 1234\nHaltungsinspektion 1234-5678"));

    [Fact]
    public void ErkenneText_DichtheitspruefungNachSia190()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Dichtheitspruefung nach SIA190:2017 / VSA RL Dicht:2023\nvon Schacht: 10081\nnach Schacht: 8993",
            "048473_DP_Gross.pdf");

        Assert.Equal(PdfDokumentTyp.Dichtheitspruefung, typ);
    }

    [Fact]
    public void ErkenneText_TvProtokollVorPlanfragment()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Haltungsinspektion - 22.06.2026 - 10081-8993\nLeitungsbericht\nLeitungsende",
            "Gesamtprotokoll.pdf");

        Assert.Equal(PdfDokumentTyp.TvProtokoll, typ);
    }

    [Fact]
    public void ErkenneText_PlanSituation()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "DW\nLeitungsende Veschlossen\nDachwasser angeschlossen",
            "AWU_Altdorf_Vorstadt_Plan.pdf");

        Assert.Equal(PdfDokumentTyp.PlanSituation, typ);
    }

    [Fact]
    public void ErkenneText_Deckblatt()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Deckblatt\nProjektuebersicht Vorstadt",
            "048473_Deckblatt.pdf");

        Assert.Equal(PdfDokumentTyp.Deckblatt, typ);
    }

    [Fact]
    public void ErkenneText_UnbekanntOhneMarker()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText("beliebiger Text", "Dokument.pdf");

        Assert.Equal(PdfDokumentTyp.Unbekannt, typ);
    }

    [Fact]
    public void ErkenneText_DpDateinameAlleinReichtNicht()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText("beliebiger Text", "048473_DP.pdf");

        Assert.Equal(PdfDokumentTyp.Unbekannt, typ);
    }

    [Fact]
    public void ErkenneDatei_Beruecksichtigt_Nur_die_angeforderte_Seitenzahl()
    {
        var path = Path.Combine(Path.GetTempPath(), "pdf-typ-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(PageSize.A4)
                .AddText("Beliebiger Inhalt", 12, new PdfPoint(40, 780), font);
            builder.AddPage(PageSize.A4)
                .AddText("Dichtheitspruefung nach SIA 190", 12, new PdfPoint(40, 780), font);
            File.WriteAllBytes(path, builder.Build());

            var firstPageOnly = PdfDokumentTypErkennung.ErkenneDatei(path, maxPages: 1);
            var bothPages = PdfDokumentTypErkennung.ErkenneDatei(path, maxPages: 2);

            Assert.Equal(PdfDokumentTyp.Unbekannt, firstPageOnly);
            Assert.Equal(PdfDokumentTyp.Dichtheitspruefung, bothPages);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Instanzdienst_liefert_bestehenden_Textdatei_Rueckfall()
    {
        var path = Path.Combine(Path.GetTempPath(), "pdf-text-prefix-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            File.WriteAllText(path, "Deckblatt\nProjektuebersicht");

            var text = new PdfTextPrefixReaderService().ReadPdfTextPrefix(path, maxPages: 2);

            Assert.Contains("Deckblatt", text);
            Assert.Contains("Projektuebersicht", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

/// <summary>
/// Echter Fall aus dem Projekt Hellgasse: KIT-Prüfberichte_2.pdf blieb "Unbekannt"
/// und wurde deshalb nie verteilt. Der Text schreibt die Norm als "SIA Norm 190"
/// bzw. "SIANorm 190" - die Erkennung suchte nur "SIA 190".
/// </summary>
public class PdfDokumentTypErkennungNormSchreibweiseTests
{
    [Theory]
    [InlineData("Feststellung der Dichtheit des oben angefuhrten Prufgegenstandes gemaess SIA Norm 190 : 2017 / PV: Luft")]
    [InlineData("Feststellung der Dichtheit des oben angefuhrten Prufgegenstandes gemaess SIANorm 190 : 2017")]
    // So liest der produktive PdfPig-Leser: er entfernt SAEMTLICHE Leerzeichen.
    // Genau daran scheiterte KIT-Pruefberichte_2.pdf im Projekt Hellgasse.
    [InlineData("FeststellungderDichtheitdesobenangefuhrtenPrufgegenstandesgemaRSIANorm190:2017/PV:LuftPrufdruck:200.0mbar")]
    public void NormMitZusatzwortNorm_GiltAlsDichtheitspruefung(string text)
    {
        Assert.Equal(
            PdfDokumentTyp.Dichtheitspruefung,
            PdfDokumentTypErkennung.ErkenneText(text, fileName: null));
    }

    [Fact]
    public void FremdeNormNummer_BleibtUnbekannt()
    {
        // Gegenprobe: keine pauschale "Norm"-Erkennung.
        Assert.NotEqual(
            PdfDokumentTyp.Dichtheitspruefung,
            PdfDokumentTypErkennung.ErkenneText("Ausgefuehrt nach SIA Norm 205", fileName: null));
        Assert.NotEqual(
            PdfDokumentTyp.Dichtheitspruefung,
            PdfDokumentTypErkennung.ErkenneText("AusgefuehrtnachSIANorm205", fileName: null));
    }

    // -----------------------------------------------------------------
    // Buerglen 2026-09-09: Die zehn Dichtheitspruefungen der Quelle heissen "DP H66.pdf"
    // und sind reine Scans ohne Textebene. Weder das Kuerzel noch der Inhalt wurden
    // erkannt — alle zehn landeten im Schachtordner statt bei ihrer Haltung.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("DP H66.pdf")]
    [InlineData("DP_H66.pdf")]
    [InlineData("2026-08-17 DP H66.pdf")]
    public void ErkenneText_DpImDateinamenGiltAlsDichtheitspruefung(string dateiname)
        => Assert.Equal(
            PdfDokumentTyp.Dichtheitspruefung,
            PdfDokumentTypErkennung.ErkenneText(text: null, dateiname));

    [Theory]
    [InlineData("Adapterplan.pdf")]
    [InlineData("Deponie DPS Bericht.pdf")]
    [InlineData("Schachtprotokoll 60248.pdf")]
    public void ErkenneText_DpNurAlsEigenesWort(string dateiname)
        => Assert.NotEqual(
            PdfDokumentTyp.Dichtheitspruefung,
            PdfDokumentTypErkennung.ErkenneText(text: null, dateiname));

    [Fact]
    public void ErkenneText_DpKuerzelGiltNurBeimScanOhneTextebene()
    {
        // Ist Text lesbar, entscheidet weiterhin allein der Inhalt: Das Kuerzel im
        // Dateinamen darf ein Schachtprotokoll nicht zur Dichtheitspruefung machen.
        Assert.Equal(
            PdfDokumentTyp.Schachtprotokoll,
            PdfDokumentTypErkennung.ErkenneText("Schachtprotokoll Schacht Nr. 60248", "DP H66.pdf"));
    }

    [Fact]
    public void ErkenneText_DruckpruefprotokollOhneDasWortDichtheit()
    {
        // Wortlaut des realen Scans (GKS Cahenzli, per OCR gelesen).
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Druckpruefprotokoll\nVon Schacht: 60248\nBis Schacht: 60247\nHaltung: H66\n"
            + "Pruefstrecke [m]: 22.80\nNorm: SIA 190",
            fileName: null);

        Assert.Equal(PdfDokumentTyp.Dichtheitspruefung, typ);
    }

    [Fact]
    public void ErkenneText_AushaerteprotokollIstEinEigenerTyp()
    {
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Aushaerteprotokoll\nHaltung: H66\nLinertyp: S+ Standard\nLampenleistung [w]: 650",
            fileName: null);

        Assert.Equal(PdfDokumentTyp.Aushaerteprotokoll, typ);
    }

    [Theory]
    [InlineData("Aushärteprotokoll\nHaltung: H66")]
    [InlineData("Aushärtungsprotokoll\nHaltung: H66")]
    [InlineData("AushaertungsprotokollHaltungH66")]
    // Reale OCR-Lesungen desselben Titels aus dem Buerglen-Bestand: Der Umlaut in der
    // grossen Titelschrift wird unzuverlaessig erkannt. Ohne diese Toleranz fielen drei
    // von neun Aushaerteprotokollen still aus der Verteilung.
    [InlineData("Aushirteprotokoll\nHaltung: H14")]
    [InlineData("Aushfarteprotokoll\nHaltung: H12 H13")]
    public void ErkenneText_AushaertungInAllenBelegtenSchreibweisen(string text)
        => Assert.Equal(PdfDokumentTyp.Aushaerteprotokoll, PdfDokumentTypErkennung.ErkenneText(text));

    [Fact]
    public void ErkenneText_AushaertungAuchOhneLesbarenTitel()
    {
        // Zweiter, umlautfreier Beleg: Lampenleistung und Linertyp gibt es nur im
        // Aushaerteprotokoll. Er traegt, wenn die Titelzeile ganz verlesen wurde.
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "GKS Cahenzli AG\nHaltung: H73 H74\nLinertyp: S+ Standard\nLampenleistung [w]: 650");

        Assert.Equal(PdfDokumentTyp.Aushaerteprotokoll, typ);
    }

    [Fact]
    public void ErkenneText_EinzelnesLampenwortMachtNochKeinAushaerteprotokoll()
        => Assert.NotEqual(
            PdfDokumentTyp.Aushaerteprotokoll,
            PdfDokumentTypErkennung.ErkenneText("Rechnung Position Lampenleistung 650 W"));

    [Fact]
    public void ErkenneText_AushaerteprotokollIstKeineDichtheitspruefung()
    {
        // Beide sind Begleitprotokolle derselben Sanierung und tragen aehnliche
        // Kopfdaten. Ein Aushaerteprotokoll hat aber keinen Pruefdruck und keine
        // Schaechte — es darf nicht als Dichtheitspruefung durchgehen.
        var typ = PdfDokumentTypErkennung.ErkenneText(
            "Aushaerteprotokoll\nHaltung: H66\nDruck p(t) [mbar]\nRohrdurchmesser [mm]: 300",
            "Aushärtungsprotokoll H66.pdf");

        Assert.Equal(PdfDokumentTyp.Aushaerteprotokoll, typ);
    }
}
