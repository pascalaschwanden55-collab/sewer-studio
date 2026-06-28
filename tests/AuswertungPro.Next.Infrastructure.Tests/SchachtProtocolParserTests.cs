using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer SchachtProtocolParser (aus LegacyPdfImportService extrahiert).
/// </summary>
public sealed class SchachtProtocolParserTests
{
    // --- ParseSchachtFields ---

    [Fact]
    public void ParseSchachtFields_ExtractsSchachtnummerDatumFunktion()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Datum 02/10/2025"
        });

        var result = SchachtProtocolParser.ParseSchachtFields(text);

        Assert.Equal("74467", result.SchachtNummer);
        Assert.Equal("02.10.2025", result.Datum);
        Assert.Equal("Kontrollschacht", result.Funktion);
    }

    [Fact]
    public void ParseSchachtFields_ReturnsEmpty_WhenTextIsWhitespace()
    {
        var result = SchachtProtocolParser.ParseSchachtFields("   ");

        Assert.Null(result.SchachtNummer);
        Assert.Null(result.Datum);
    }

    // --- DeriveSchachtStatus ---

    [Fact]
    public void DeriveSchachtStatus_ReturnsOffen_WhenDamagesExistAndNoExplicitStatus()
    {
        var status = SchachtProtocolParser.DeriveSchachtStatus("Deckelrahmen: gerissen", "keine Status-Zeile hier");

        Assert.Equal("offen", status);
    }

    [Fact]
    public void DeriveSchachtStatus_ReturnsAbgeschlossen_WhenMaengelfrei()
    {
        var status = SchachtProtocolParser.DeriveSchachtStatus("Maengelfrei", "keine Status-Zeile");

        Assert.Equal("abgeschlossen", status);
    }

    [Fact]
    public void DeriveSchachtStatus_PrefersExplicitStatus_OverDerivedStatus()
    {
        var text = "Status offen/abgeschlossen: abgeschlossen";
        var status = SchachtProtocolParser.DeriveSchachtStatus("Deckelrahmen: gerissen", text);

        Assert.Equal("abgeschlossen", status);
    }

    [Fact]
    public void DeriveSchachtStatus_ReturnsNull_WhenNoDamagesAndNoExplicitStatus()
    {
        var status = SchachtProtocolParser.DeriveSchachtStatus(null, "kein Hinweis");

        Assert.Null(status);
    }

    // --- TryParseExplicitStatus ---

    [Fact]
    public void TryParseExplicitStatus_DetectsAbgeschlossen()
    {
        var result = SchachtProtocolParser.TryParseExplicitStatus("Status offen/abgeschlossen: abgeschlossen");

        Assert.Equal("abgeschlossen", result);
    }

    [Fact]
    public void TryParseExplicitStatus_DetectsOffen()
    {
        var result = SchachtProtocolParser.TryParseExplicitStatus("Status: offen");

        Assert.Equal("offen", result);
    }

    [Fact]
    public void TryParseExplicitStatus_ReturnsNull_WhenNoStatusLine()
    {
        var result = SchachtProtocolParser.TryParseExplicitStatus("Datum: 01.01.2025");

        Assert.Null(result);
    }

    // --- NormalizeCheckboxGlyphs ---

    [Fact]
    public void NormalizeCheckboxGlyphs_ConvertsBullet()
    {
        var result = SchachtProtocolParser.NormalizeCheckboxGlyphs("• test");

        Assert.Contains("●", result);
    }

    [Fact]
    public void NormalizeCheckboxGlyphs_PassesThroughNormalText()
    {
        var input = "Deckelrahmen gerissen";
        var result = SchachtProtocolParser.NormalizeCheckboxGlyphs(input);

        Assert.Equal(input, result);
    }

    // --- TryExtractComponentTail ---

    [Fact]
    public void TryExtractComponentTail_MatchesDeckelrahmen()
    {
        var success = SchachtProtocolParser.TryExtractComponentTail(
            "Deckelrahmen gerissen ausgebrochen",
            out var component,
            out var tail);

        Assert.True(success);
        Assert.Equal("Deckelrahmen", component);
        Assert.Contains("gerissen", tail);
    }

    [Fact]
    public void TryExtractComponentTail_ReturnsFalse_ForUnknownLine()
    {
        var success = SchachtProtocolParser.TryExtractComponentTail(
            "Kanalrohr beschaedigt",
            out _,
            out _);

        Assert.False(success);
    }

    // --- GetDamageCandidatesForComponent ---

    [Fact]
    public void GetDamageCandidatesForComponent_ReturnsKnownCandidates_ForSchachtdeckel()
    {
        var candidates = SchachtProtocolParser.GetDamageCandidatesForComponent("Schachtdeckel");

        Assert.Contains("gerissen", candidates);
        Assert.Contains("korrodiert", candidates);
    }

    [Fact]
    public void GetDamageCandidatesForComponent_ReturnsEmpty_ForUnknownComponent()
    {
        var candidates = SchachtProtocolParser.GetDamageCandidatesForComponent("Unbekannt");

        Assert.Empty(candidates);
    }

    // --- IsMarkedDamage ---

    [Fact]
    public void IsMarkedDamage_DetectsBulletBeforeDamage()
    {
        var result = SchachtProtocolParser.IsMarkedDamage("● gerissen ausgebrochen", "gerissen");

        Assert.True(result);
    }

    [Fact]
    public void IsMarkedDamage_DetectsBracketMarker()
    {
        var result = SchachtProtocolParser.IsMarkedDamage("[x] fehlt", "fehlt");

        Assert.True(result);
    }

    [Fact]
    public void IsMarkedDamage_ReturnsFalse_WhenNoMarkerPresent()
    {
        var result = SchachtProtocolParser.IsMarkedDamage("gerissen ausgebrochen", "gerissen");

        Assert.False(result);
    }

    // --- GetComponentOrderIndex ---

    [Fact]
    public void GetComponentOrderIndex_ReturnsCorrectOrder()
    {
        var deckelIdx = SchachtProtocolParser.GetComponentOrderIndex("Schachtdeckel");
        var schachthalsIdx = SchachtProtocolParser.GetComponentOrderIndex("Schachthals");

        Assert.True(deckelIdx < schachthalsIdx);
    }

    [Fact]
    public void GetComponentOrderIndex_ReturnsMaxValue_ForUnknown()
    {
        var idx = SchachtProtocolParser.GetComponentOrderIndex("Unbekannt");

        Assert.Equal(int.MaxValue, idx);
    }

    // --- NormalizeDate ---

    [Fact]
    public void NormalizeDate_ParsesSlashFormat()
    {
        var result = SchachtProtocolParser.NormalizeDate("02/10/2025");

        Assert.Equal("02.10.2025", result);
    }

    [Fact]
    public void NormalizeDate_ReturnsNull_ForNullOrEmpty()
    {
        Assert.Null(SchachtProtocolParser.NormalizeDate(null));
        Assert.Null(SchachtProtocolParser.NormalizeDate(""));
    }

    [Fact]
    public void NormalizeDate_ReturnsRaw_WhenNotParseable()
    {
        var result = SchachtProtocolParser.NormalizeDate("kein Datum");

        Assert.Equal("kein Datum", result);
    }

    // --- ParseSchachtDamageEntries ---

    [Fact]
    public void ParseSchachtDamageEntries_ExtractsBulletMarkedDamage()
    {
        var text = "Schachtdeckel ● gerissen ausgebrochen";

        var entries = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        Assert.Single(entries);
        Assert.Equal("Schachtdeckel", entries[0].Component);
        Assert.Equal("gerissen", entries[0].Damage);
    }

    [Fact]
    public void ParseSchachtDamageEntries_ReturnsEmpty_WhenNoMarkedDamages()
    {
        var text = "Schachtdeckel gerissen ausgebrochen";

        var entries = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        Assert.Empty(entries);
    }

    [Fact]
    public void ParseSchachtDamageEntries_DeduplicatesEntries()
    {
        var text = string.Join("\n", new[]
        {
            "Deckelrahmen ● gerissen ausgebrochen",
            "Deckelrahmen gerissen ● ausgebrochen"
        });

        var entries = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        var gerissen = entries.Where(e => e.Component == "Deckelrahmen" && e.Damage == "gerissen").ToList();
        Assert.Single(gerissen);
    }

    [Fact]
    public void ParseSchachtDamageEntries_OrdersByComponentThenDamage()
    {
        var text = string.Join("\n", new[]
        {
            "Schachthals ● gerissen",
            "Deckelrahmen ● ausgebrochen"
        });

        var entries = SchachtProtocolParser.ParseSchachtDamageEntries(text);

        Assert.True(entries.Count >= 2);
        // Deckelrahmen comes before Schachthals in SchachtComponentOrder
        var deckelIdx = Array.FindIndex(entries.ToArray(), e => e.Component == "Deckelrahmen");
        var schachthalsIdx = Array.FindIndex(entries.ToArray(), e => e.Component == "Schachthals");
        Assert.True(deckelIdx < schachthalsIdx);
    }
}
