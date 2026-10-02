using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Die eine Stelle fuer die Quellvorgabe des Pruefplatzes (B6, Deepscan 02.10.2026).</summary>
public sealed class WorkbenchSourceSuggestionFactoryTests
{
    private static TrainingSample PdfSample() => new()
    {
        SourceType = SourceTypeNames.PdfPhoto,
        SourceReferenceCode = "BAB",
        SourceReferenceDescription = "Riss quer im Scheitel",
        InspectionDate = new DateTime(2023, 11, 23),
        Notes =
            "PDF-Operateurreferenz: 20231123_06.887943-90327.pdf; " +
            "SHA-256=8a7cfb71d1289694b8a650fe2c49357840fe1935ac120b8fb83d24f899c99c6f; " +
            "Seite=3; Foto=42; Zuordnung=photo_id"
    };

    [Fact]
    public void PDF_Foto_mit_Herkunftsvermerk_liefert_die_Vorgabe()
    {
        var vorgabe = WorkbenchSourceSuggestionFactory.Von(PdfSample());

        Assert.NotNull(vorgabe);
        Assert.Equal("BAB", vorgabe!.VsaCode);
        Assert.Equal("Riss quer im Scheitel", vorgabe.Beschreibung);
        Assert.Equal("20231123_06.887943-90327.pdf", vorgabe.SourceDocumentName);
        Assert.Equal(3, vorgabe.PageNumber);
        Assert.Equal("42", vorgabe.PhotoId);
        Assert.Equal("photo_id", vorgabe.MatchKind);
        Assert.Equal(new DateTime(2023, 11, 23), vorgabe.InspectionDate);
    }

    [Fact]
    public void Andere_Quelle_fehlender_Vermerk_Code_oder_Beschreibung_liefern_null()
    {
        var andereQuelle = PdfSample();
        andereQuelle.SourceType = "video";
        Assert.Null(WorkbenchSourceSuggestionFactory.Von(andereQuelle));

        var ohneVermerk = PdfSample();
        ohneVermerk.Notes = "nur eine Notiz";
        Assert.Null(WorkbenchSourceSuggestionFactory.Von(ohneVermerk));

        var ohneCode = PdfSample();
        ohneCode.SourceReferenceCode = " ";
        Assert.Null(WorkbenchSourceSuggestionFactory.Von(ohneCode));

        var ohneBeschreibung = PdfSample();
        ohneBeschreibung.SourceReferenceDescription = null;
        Assert.Null(WorkbenchSourceSuggestionFactory.Von(ohneBeschreibung));
    }
}
