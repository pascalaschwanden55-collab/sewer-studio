using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer PdfPathMetadataExtractor (aus LegacyPdfImportService extrahiert).
/// </summary>
public sealed class PdfPathMetadataExtractorTests
{
    // --- TryExtractHoldingIdFromName ---

    [Fact]
    public void TryExtractHoldingIdFromName_ExtractsDashPair()
    {
        var result = PdfPathMetadataExtractor.TryExtractHoldingIdFromName("29120-03.27666_Inspektion");

        Assert.NotNull(result);
        Assert.Contains("-", result);
    }

    [Fact]
    public void TryExtractHoldingIdFromName_ExtractsUnderscorePair()
    {
        var result = PdfPathMetadataExtractor.TryExtractHoldingIdFromName("32953_1225");

        Assert.Equal("32953-1225", result);
    }

    [Fact]
    public void TryExtractHoldingIdFromName_ReturnsNull_ForDateName()
    {
        // Datierter Name soll die Haltungs-ID behalten, nicht das Datum als ID liefern
        var result = PdfPathMetadataExtractor.TryExtractHoldingIdFromName("20250630");

        // Kein Bindestrich => null
        Assert.Null(result);
    }

    [Fact]
    public void TryExtractHoldingIdFromName_ReturnsNull_ForEmptyString()
    {
        Assert.Null(PdfPathMetadataExtractor.TryExtractHoldingIdFromName(""));
        Assert.Null(PdfPathMetadataExtractor.TryExtractHoldingIdFromName(null!));
    }

    // --- TryExtractDateFromName ---

    [Fact]
    public void TryExtractDateFromName_ParsesYyyyMmDd()
    {
        var result = PdfPathMetadataExtractor.TryExtractDateFromName("20250630_Protokoll");

        Assert.Equal(new DateTime(2025, 6, 30), result);
    }

    [Fact]
    public void TryExtractDateFromName_ParsesDdMmYyyy()
    {
        var result = PdfPathMetadataExtractor.TryExtractDateFromName("Inspektion_30.06.2025");

        Assert.Equal(new DateTime(2025, 6, 30), result);
    }

    [Fact]
    public void TryExtractDateFromName_ReturnsNull_ForNonDate()
    {
        var result = PdfPathMetadataExtractor.TryExtractDateFromName("HaltungABC");

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractDateFromName_ReturnsNull_ForEmpty()
    {
        Assert.Null(PdfPathMetadataExtractor.TryExtractDateFromName(""));
    }

    // --- TryExtractHoldingIdFromFileName ---

    [Fact]
    public void TryExtractHoldingIdFromFileName_ExtractsFromDashedName()
    {
        var path = @"C:\Inspektionen\29120-03.27666.pdf";
        var result = PdfPathMetadataExtractor.TryExtractHoldingIdFromFileName(path);

        Assert.NotNull(result);
        Assert.Contains("-", result);
    }

    [Fact]
    public void TryExtractHoldingIdFromFileName_ReturnsNull_ForPlainName()
    {
        var path = @"C:\Inspektionen\Protokoll.pdf";
        var result = PdfPathMetadataExtractor.TryExtractHoldingIdFromFileName(path);

        Assert.Null(result);
    }

    // --- TryExtractDateFromFileName ---

    [Fact]
    public void TryExtractDateFromFileName_ExtractsYyyyMmDdFromName()
    {
        var path = @"C:\Inspektionen\20250630_Protokoll.pdf";
        var result = PdfPathMetadataExtractor.TryExtractDateFromFileName(path);

        Assert.Equal(new DateTime(2025, 6, 30), result);
    }

    [Fact]
    public void TryExtractDateFromFileName_ReturnsNull_WhenNoDate()
    {
        var path = @"C:\Inspektionen\Protokoll.pdf";
        var result = PdfPathMetadataExtractor.TryExtractDateFromFileName(path);

        Assert.Null(result);
    }

    // --- ApplyPathDateFallbackCore ---

    [Fact]
    public void ApplyPathDateFallbackCore_SetsDatumJahr_WhenMissing()
    {
        var fields = new Dictionary<string, string>();
        var path = @"C:\Inspektionen\20250630_29120-03.27666.pdf";

        PdfPathMetadataExtractor.ApplyPathDateFallbackCore(fields, "29120-03.27666", path);

        Assert.True(fields.ContainsKey("Datum_Jahr"));
        Assert.Equal("30.06.2025", fields["Datum_Jahr"]);
    }

    [Fact]
    public void ApplyPathDateFallbackCore_DoesNotOverwrite_ExistingDatum()
    {
        var fields = new Dictionary<string, string> { ["Datum_Jahr"] = "01.01.2020" };
        var path = @"C:\Inspektionen\20250630_29120-03.27666.pdf";

        PdfPathMetadataExtractor.ApplyPathDateFallbackCore(fields, "29120-03.27666", path);

        Assert.Equal("01.01.2020", fields["Datum_Jahr"]);
    }

    [Fact]
    public void ApplyPathDateFallbackCore_DoesNotSet_WhenKeyNotHoldingId()
    {
        var fields = new Dictionary<string, string>();
        var path = @"C:\Inspektionen\20250630_Protokoll.pdf";

        PdfPathMetadataExtractor.ApplyPathDateFallbackCore(fields, "NichtEineHaltungsId", path);

        Assert.False(fields.ContainsKey("Datum_Jahr"));
    }
}
