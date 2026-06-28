using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer PdfPlaceholderClassifier (aus LegacyPdfImportService extrahiert).
/// </summary>
public sealed class PdfPlaceholderClassifierTests
{
    // --- NormalizeForFingerprint ---

    [Fact]
    public void NormalizeForFingerprint_CollapsesWhitespace()
    {
        var result = PdfPlaceholderClassifier.NormalizeForFingerprint("  viel   Platz  ");

        Assert.Equal("viel Platz", result);
    }

    [Fact]
    public void NormalizeForFingerprint_ReturnsEmpty_ForNull()
    {
        Assert.Equal("", PdfPlaceholderClassifier.NormalizeForFingerprint(null));
    }

    [Fact]
    public void NormalizeForFingerprint_ReturnsEmpty_ForBlank()
    {
        Assert.Equal("", PdfPlaceholderClassifier.NormalizeForFingerprint("   "));
    }

    // --- HasMeaningfulText ---

    [Fact]
    public void HasMeaningfulText_ReturnsTrueForNormalText()
    {
        Assert.True(PdfPlaceholderClassifier.HasMeaningfulText("BAA - Verformung"));
    }

    [Fact]
    public void HasMeaningfulText_ReturnsFalseForNull()
    {
        Assert.False(PdfPlaceholderClassifier.HasMeaningfulText(null));
    }

    [Fact]
    public void HasMeaningfulText_ReturnsFalseForWhitespace()
    {
        Assert.False(PdfPlaceholderClassifier.HasMeaningfulText("   "));
    }

    [Fact]
    public void HasMeaningfulText_ReturnsTrueForDigitsOnly()
    {
        Assert.True(PdfPlaceholderClassifier.HasMeaningfulText("300"));
    }

    // --- IsHeaderPlaceholderKey ---

    [Fact]
    public void IsHeaderPlaceholderKey_DetectsWetterDatumKey()
    {
        var key = "Datum : 01.01.2025 Wetter : sonnig Operator : Meier";
        Assert.True(PdfPlaceholderClassifier.IsHeaderPlaceholderKey(key));
    }

    [Fact]
    public void IsHeaderPlaceholderKey_ReturnsFalseForNormalKey()
    {
        Assert.False(PdfPlaceholderClassifier.IsHeaderPlaceholderKey("29120-03.27666"));
    }

    [Fact]
    public void IsHeaderPlaceholderKey_ReturnsFalseForEmpty()
    {
        Assert.False(PdfPlaceholderClassifier.IsHeaderPlaceholderKey(""));
    }

    // --- IsKnownPlaceholderKey ---

    [Fact]
    public void IsKnownPlaceholderKey_DetectsUnbekanntPrefix()
    {
        Assert.True(PdfPlaceholderClassifier.IsKnownPlaceholderKey("UNBEKANNT_20250101_120000_0"));
    }

    [Fact]
    public void IsKnownPlaceholderKey_DetectsDatumColon()
    {
        Assert.True(PdfPlaceholderClassifier.IsKnownPlaceholderKey("Datum :"));
    }

    [Fact]
    public void IsKnownPlaceholderKey_DetectsHaltungsnamePlaceholder()
    {
        Assert.True(PdfPlaceholderClassifier.IsKnownPlaceholderKey("Haltungsname"));
    }

    [Fact]
    public void IsKnownPlaceholderKey_ReturnsFalseForRealHoldingId()
    {
        Assert.False(PdfPlaceholderClassifier.IsKnownPlaceholderKey("29120-03.27666"));
    }

    [Fact]
    public void IsKnownPlaceholderKey_ReturnsTrueForNullOrEmpty()
    {
        Assert.True(PdfPlaceholderClassifier.IsKnownPlaceholderKey(""));
        Assert.True(PdfPlaceholderClassifier.IsKnownPlaceholderKey(null!));
    }

    // --- BuildRepairFingerprint ---

    [Fact]
    public void BuildRepairFingerprint_ReturnsNull_WhenNoPrimaereSchaeden()
    {
        var r = new HaltungRecord();
        r.SetFieldValue("DN_mm", "300", FieldSource.Pdf, userEdited: false);

        var result = PdfPlaceholderClassifier.BuildRepairFingerprint(r);

        Assert.Null(result);
    }

    [Fact]
    public void BuildRepairFingerprint_ReturnsConsistentFingerprint()
    {
        var r1 = new HaltungRecord();
        r1.SetFieldValue("Primaere_Schaeden", "BAA - Verformung", FieldSource.Pdf, userEdited: false);
        r1.SetFieldValue("DN_mm", "300", FieldSource.Pdf, userEdited: false);

        var r2 = new HaltungRecord();
        r2.SetFieldValue("Primaere_Schaeden", "BAA - Verformung", FieldSource.Pdf, userEdited: false);
        r2.SetFieldValue("DN_mm", "300", FieldSource.Pdf, userEdited: false);

        var fp1 = PdfPlaceholderClassifier.BuildRepairFingerprint(r1);
        var fp2 = PdfPlaceholderClassifier.BuildRepairFingerprint(r2);

        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void BuildRepairFingerprint_DifferentiatesByDn()
    {
        var r1 = new HaltungRecord();
        r1.SetFieldValue("Primaere_Schaeden", "BAA - Verformung", FieldSource.Pdf, userEdited: false);
        r1.SetFieldValue("DN_mm", "300", FieldSource.Pdf, userEdited: false);

        var r2 = new HaltungRecord();
        r2.SetFieldValue("Primaere_Schaeden", "BAA - Verformung", FieldSource.Pdf, userEdited: false);
        r2.SetFieldValue("DN_mm", "400", FieldSource.Pdf, userEdited: false);

        var fp1 = PdfPlaceholderClassifier.BuildRepairFingerprint(r1);
        var fp2 = PdfPlaceholderClassifier.BuildRepairFingerprint(r2);

        Assert.NotEqual(fp1, fp2);
    }

    // --- ShouldSkipUnknownChunk ---

    [Fact]
    public void ShouldSkipUnknownChunk_ReturnsFalse_WhenFieldsHavePayload()
    {
        var fields = new Dictionary<string, string> { ["DN_mm"] = "300" };
        var chunk = new PdfChunk { Text = "", Index = 0 };

        Assert.False(PdfPlaceholderClassifier.ShouldSkipUnknownChunk(fields, chunk));
    }

    [Fact]
    public void ShouldSkipUnknownChunk_ReturnsTrue_WhenNoPayloadAndNoHaltungsrow()
    {
        var fields = new Dictionary<string, string>();
        var chunk = new PdfChunk { Text = "Seite 1 von 5", Index = 0 };

        Assert.True(PdfPlaceholderClassifier.ShouldSkipUnknownChunk(fields, chunk));
    }

    [Fact]
    public void ShouldSkipUnknownChunk_ReturnsFalse_WhenChunkHasHaltungsRow()
    {
        var fields = new Dictionary<string, string>();
        var chunk = new PdfChunk
        {
            Text = "29120-03 01.01.2025",
            Index = 0
        };

        Assert.False(PdfPlaceholderClassifier.ShouldSkipUnknownChunk(fields, chunk));
    }
}
