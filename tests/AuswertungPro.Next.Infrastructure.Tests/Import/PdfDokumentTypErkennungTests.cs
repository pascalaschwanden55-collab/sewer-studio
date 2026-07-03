using AuswertungPro.Next.Infrastructure.Import;
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
}
