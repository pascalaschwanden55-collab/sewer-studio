using AuswertungPro.Next.Infrastructure.Import;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ShaftPdfRelevanceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unbekannter Bericht")]
    [InlineData("Haltungsinspektion und Schachtprotokoll Schacht Nr. 10051")]
    [InlineData("Projekt Kunde Unternehmer Schacht Nr. 1234")]
    public void UnklareOderSchachtSeite_WirdNichtAusgeschlossen(string? text)
        => Assert.False(ShaftPdfRelevance.IsClearlyForeignPage(text));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpaeterSchachtteilOderBildseite_BleibtHinterVielenTvSeitenErhalten(bool bildseite)
    {
        var path = Path.Combine(Path.GetTempPath(), "shaft-mixed-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            for (var i = 0; i < 8; i++)
                builder.AddPage(PageSize.A4).AddText("Haltungsinspektion 1000-2000", 12, new PdfPoint(40, 780), font);
            var last = builder.AddPage(PageSize.A4);
            if (!bildseite) last.AddText("Schachtprotokoll Schacht Nr. 1234", 12, new PdfPoint(40, 780), font);
            File.WriteAllBytes(path, builder.Build());
            Assert.True(ShaftPdfRelevance.ShouldProcess(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FehlendeDatei_BleibtFuerDenNormalenFehlerberichtErhalten()
        => Assert.True(ShaftPdfRelevance.ShouldProcess(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf")));

    // -----------------------------------------------------------------
    // Buerglen 2026-09-09: Die Begleitprotokolle der Sanierung sind reine Scans ohne
    // Textebene. Sie galten damit als "unklar" und liefen in den Schachtweg; dessen OCR
    // fand die erste bekannte Nummer und legte sie beim Schacht ab. Alle zehn
    // Dichtheitspruefungen und alle neun Aushaerteprotokolle landeten so falsch.
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("Druckpruefprotokoll\nVon Schacht: 60248\nBis Schacht: 60247\nNorm: SIA 190")]
    [InlineData("Aushaerteprotokoll\nHaltung: H66\nLinertyp: S+ Standard")]
    public void ReinerScan_WirdNachSeinemKopfBeurteilt(string ocrText)
    {
        MitScan(2, path =>
            Assert.False(ShaftPdfRelevance.ShouldProcess(path, (_, _) => ocrText)));
    }

    [Fact]
    public void ReinerScan_MitSchachtinhalt_BleibtImSchachtweg()
    {
        MitScan(2, path =>
            Assert.True(ShaftPdfRelevance.ShouldProcess(
                path, (_, _) => "Schachtprotokoll Schacht Nr. 60248")));
    }

    [Fact]
    public void ReinerScan_OhneLesbaresOcr_BleibtImSchachtweg()
    {
        // Kein Beleg ist kein Beleg fuer einen fremden Inhalt — dieselbe Regel wie bei
        // einer unlesbaren Datei.
        MitScan(2, path => Assert.True(ShaftPdfRelevance.ShouldProcess(path, (_, _) => null)));
        MitScan(2, path => Assert.True(ShaftPdfRelevance.ShouldProcess(path, (_, _) => "   ")));
    }

    [Fact]
    public void ReinerScan_ZweiteSeiteEntscheidetMit()
    {
        // Erste Seite ohne Aussage, zweite nennt einen Schacht: bleibt im Schachtweg.
        MitScan(2, path =>
            Assert.True(ShaftPdfRelevance.ShouldProcess(
                path,
                (_, seite) => seite == 1 ? "Deckblatt ohne Aussage" : "Schacht Nr. 60248")));
    }

    [Fact]
    public void BildseiteZwischenTextseiten_WirdNichtGeocrt()
    {
        // Gemischtes Dokument: Hier gilt weiterhin die alte Regel, und es darf kein
        // teures OCR anlaufen.
        var path = Path.Combine(Path.GetTempPath(), "shaft-mixed-ocr-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            using var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            builder.AddPage(PageSize.A4).AddText("Haltungsinspektion 1000-2000", 12, new PdfPoint(40, 780), font);
            builder.AddPage(PageSize.A4);
            File.WriteAllBytes(path, builder.Build());

            var ocrAufrufe = 0;
            var ergebnis = ShaftPdfRelevance.ShouldProcess(path, (_, _) => { ocrAufrufe++; return "Schacht Nr. 1"; });

            Assert.True(ergebnis);
            Assert.Equal(0, ocrAufrufe);
        }
        finally { File.Delete(path); }
    }

    private static void MitScan(int seiten, Action<string> pruefung)
    {
        var path = Path.Combine(Path.GetTempPath(), "shaft-scan-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            using var builder = new PdfDocumentBuilder();
            for (var i = 0; i < seiten; i++)
                builder.AddPage(PageSize.A4);
            File.WriteAllBytes(path, builder.Build());
            pruefung(path);
        }
        finally { File.Delete(path); }
    }
}
