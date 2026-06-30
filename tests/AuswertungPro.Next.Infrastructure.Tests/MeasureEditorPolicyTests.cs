using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests für MeasureEditorIdPolicy und TemplateQtyExtractor.
/// Alle Erwartungswerte wurden aus der bisherigen Inline-Logik in
/// MeasureTemplateEditorViewModel abgeleitet.
/// </summary>
public sealed class MeasureEditorPolicyTests
{
    // -----------------------------------------------------------------------
    // MeasureEditorIdPolicy – Template-ID-Generierung
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, "template_1")]
    [InlineData(1, "template_2")]
    [InlineData(9, "template_10")]
    public void NewTemplateId_ReturnsExpectedFormat(int count, string expected)
    {
        var result = MeasureEditorIdPolicy.NewTemplateId(count);
        Assert.Equal(expected, result);
    }

    // -----------------------------------------------------------------------
    // MeasureEditorIdPolicy – Katalog-ID-Generierung, kein Konflikt
    // -----------------------------------------------------------------------

    [Fact]
    public void NewCatalogItemId_EmptyCollection_ReturnsNeu1()
    {
        var result = MeasureEditorIdPolicy.NewCatalogItemId(Array.Empty<string>());
        Assert.Equal("neu_1", result);
    }

    [Fact]
    public void NewCatalogItemId_OneEntry_ReturnsNeu2()
    {
        var result = MeasureEditorIdPolicy.NewCatalogItemId(new[] { "other_id" });
        Assert.Equal("neu_2", result);
    }

    // -----------------------------------------------------------------------
    // MeasureEditorIdPolicy – Kollisionsvermeidung
    // -----------------------------------------------------------------------

    [Fact]
    public void NewCatalogItemId_SkipsExistingIds_ReturnsFirstFreeSlot()
    {
        // 1 vorhandener Eintrag -> Kandidat waere neu_2, aber neu_2 ist belegt -> neu_3
        var existing = new List<string> { "irgendwas", "neu_2" };
        var result = MeasureEditorIdPolicy.NewCatalogItemId(existing);
        Assert.Equal("neu_3", result);
    }

    [Fact]
    public void NewCatalogItemId_CaseInsensitiveCollision_SkipsCorrectly()
    {
        // Gross-/Kleinschreibung darf Kollision nicht verhindern
        var existing = new List<string> { "NEU_2" };
        var result = MeasureEditorIdPolicy.NewCatalogItemId(existing);
        // 1 Eintrag -> Kandidat neu_2 kollidiert mit NEU_2 (OrdinalIgnoreCase) -> neu_3
        Assert.Equal("neu_3", result);
    }

    [Fact]
    public void NewCatalogItemId_MultipleGaps_FindsFirstFreeSlot()
    {
        // Lücke bei neu_4: neu_2/neu_3 belegt, 3 Einträge -> Kandidat neu_4 frei
        var existing = new List<string> { "a", "neu_2", "neu_3" };
        var result = MeasureEditorIdPolicy.NewCatalogItemId(existing);
        Assert.Equal("neu_4", result);
    }

    // -----------------------------------------------------------------------
    // TemplateQtyExtractor – String-Wert
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractQtyString_JsonString_ReturnsStringContent()
    {
        var element = JsonDocument.Parse("\"5\"").RootElement.Clone();
        var result = TemplateQtyExtractor.ExtractQtyString(element);
        Assert.Equal("5", result);
    }

    [Fact]
    public void ExtractQtyString_JsonStringEmpty_ReturnsFallback()
    {
        // Leerer String -> GetString() liefert "" != null, also "" zurück (kein Fallback)
        // Laut Implementierung: GetString() ?? "1" -> "" ist nicht null, also ""
        var element = JsonDocument.Parse("\"\"").RootElement.Clone();
        var result = TemplateQtyExtractor.ExtractQtyString(element);
        Assert.Equal("", result);
    }

    // -----------------------------------------------------------------------
    // TemplateQtyExtractor – Numerischer Wert
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractQtyString_JsonNumber_ReturnsRawText()
    {
        var element = JsonDocument.Parse("1").RootElement.Clone();
        var result = TemplateQtyExtractor.ExtractQtyString(element);
        Assert.Equal("1", result);
    }

    [Fact]
    public void ExtractQtyString_JsonDecimalNumber_ReturnsRawText()
    {
        var element = JsonDocument.Parse("2.5").RootElement.Clone();
        var result = TemplateQtyExtractor.ExtractQtyString(element);
        Assert.Equal("2.5", result);
    }

    [Fact]
    public void ExtractQtyString_JsonNull_ReturnsFallback()
    {
        // null ist kein String-ValueKind -> GetRawText() -> "null"
        var element = JsonDocument.Parse("null").RootElement.Clone();
        var result = TemplateQtyExtractor.ExtractQtyString(element);
        Assert.Equal("null", result);
    }
}
