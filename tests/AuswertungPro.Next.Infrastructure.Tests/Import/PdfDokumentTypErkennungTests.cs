using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class PdfDokumentTypErkennungTests
{
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
