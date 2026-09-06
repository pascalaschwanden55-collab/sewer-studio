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
}
