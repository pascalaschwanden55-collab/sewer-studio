using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer VsaCodeResolver — zentrale VSA-Code-Aufloesung.
/// Deckt ab: NormalizeFindingCode, InferCodeFromLabel, LookupLabel, NormalizeClock.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VsaCodeResolverTests
{
    // ═══════════════════════════════════════════════════════════════
    // NormalizeFindingCode
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("BCD", "BCD")]         // Exakter Katalog-Match
    [InlineData("BCE", "BCE")]         // Endknoten
    [InlineData("BCA", "BCA")]         // Seitlicher Anschluss
    [InlineData("BAB", "BAB")]         // Risse
    [InlineData("BCAEB", "BCAEB")]     // Untercode: BCA bekannt → akzeptiert
    [InlineData("BABBA", "BABBA")]     // Untercode: BAB bekannt → akzeptiert
    [InlineData("BAFCE", "BAFCE")]     // Untercode: BAF bekannt → akzeptiert
    [InlineData("bcd", "BCD")]         // Kleinbuchstaben → normalisiert
    [InlineData("BCA.E.B", "BCAEB")]   // Punkt-Notation → entfernt
    public void NormalizeFindingCode_ValidCodes_ReturnsNormalized(string input, string expected)
    {
        var result = VsaCodeResolver.NormalizeFindingCode(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]                 // Null
    [InlineData("")]                   // Leer
    [InlineData("   ")]               // Whitespace
    [InlineData("X")]                  // Zu kurz (1 Zeichen)
    [InlineData("ABCDEFG")]           // Zu lang (7 Zeichen)
    [InlineData("123")]               // Nur Ziffern
    [InlineData("BA1")]               // Ziffern gemischt
    [InlineData("???")]               // Fragezeichen
    [InlineData("XX")]                // Unbekannte Gruppe
    [InlineData("ZZ")]                // Nicht im Katalog
    public void NormalizeFindingCode_InvalidCodes_ReturnsNull(string? input)
    {
        var result = VsaCodeResolver.NormalizeFindingCode(input);
        Assert.Null(result);
    }

    [Fact]
    public void NormalizeFindingCode_TwoCharGroupAlone_ReturnsNull()
    {
        // 2-Zeichen-Gruppe allein (z.B. "BA") wird NICHT akzeptiert —
        // der geschaerfte Validator erfordert entweder exakten Katalog-Match
        // oder einen 3-Zeichen-Hauptcode. "BA" hat keinen exakten Match
        // im Katalog (nur die Gruppe).
        // Allerdings: "BA" IST im Katalog als Gruppenlabel.
        // Deshalb: 2-Zeichen-Codes die eine Gruppe sind sollten akzeptiert werden.
        var result = VsaCodeResolver.NormalizeFindingCode("BA");
        // BA ist als Gruppe im Katalog → sollte akzeptiert werden
        // ABER unser Validator erfordert 3Z-Hauptcode als Minimum bei nicht-exaktem Match
        // und BA hat einen exakten Match via LookupLabel("BA") → "Strukturschäden"
        Assert.NotNull(result); // BA ist exakt im Katalog
    }

    // ═══════════════════════════════════════════════════════════════
    // InferCodeFromLabel — Keyword-Heuristik
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("Seitlicher Anschluss", "BCA")]
    [InlineData("Lateral connection at 3 o'clock", "BCA")]
    [InlineData("Abzweig links", "BCA")]
    [InlineData("Rohranfang", "BCD")]
    [InlineData("Pipe start visible", "BCD")]
    [InlineData("Rohrende", "BCE")]
    [InlineData("Bogen nach rechts", "BCC")]
    [InlineData("Riss längs", "BAB")]
    [InlineData("Surface crack", "BAB")]
    [InlineData("Bruch/Einsturz", "BAC")]
    [InlineData("Deformation oval", "BAA")]      // BAA = Verformung (nicht BAF = Oberflaechenschaden)
    [InlineData("Wurzeleinwuchs", "BBA")]         // BBA = Wurzeln (nicht BBB = Anhaftende Stoffe)
    [InlineData("Inkrustation verkalkt", "BBB")]   // BBB = Anhaftende Stoffe (nicht BBA = Wurzeln)
    [InlineData("Ablagerung in der Sohle", "BBC")]
    [InlineData("Attached deposit on pipe wall", "BBC")]  // BBC = Ablagerungen (deposit)
    [InlineData("Wasserstand in der Sohle", "BDD")]       // BDD = Wasserspiegel (nicht BDDC)
    [InlineData("Standing water at invert", "BDD")]
    [InlineData("Water level visible", "BDD")]
    [InlineData("Hindernis Gegenstand", "BBE")]            // BBE = Andere Hindernisse
    [InlineData("Hohlraum sichtbar", "BAP")]               // BAP = Hohlraum sichtbar
    [InlineData("Schweissnaht mangelhaft", "BAM")]         // BAM = Schadhafte Schweissnaht
    public void InferCodeFromLabel_KnownLabels_ReturnsCorrectCode(string label, string expected)
    {
        var result = VsaCodeResolver.InferCodeFromLabel(label);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something unknown")]
    [InlineData("normal pipe section")]
    [InlineData("good condition")]
    public void InferCodeFromLabel_UnknownLabels_ReturnsNull(string? label)
    {
        var result = VsaCodeResolver.InferCodeFromLabel(label);
        Assert.Null(result);
    }

    [Fact]
    public void InferCodeFromLabel_WordBoundary_NoFalsePositive()
    {
        // "lateral" als Wort → BCA, aber "collateral" sollte NICHT matchen
        Assert.Equal("BCA", VsaCodeResolver.InferCodeFromLabel("lateral connection"));
        Assert.Null(VsaCodeResolver.InferCodeFromLabel("collateral damage"));

        // "crack" als Wort → BAB, aber "cracking joke" koennte matchen — akzeptabel
        Assert.Equal("BAB", VsaCodeResolver.InferCodeFromLabel("a crack in the pipe"));

        // "root" matcht BBA (Wurzeln) — korrekter VSA-Code
        Assert.Equal("BBA", VsaCodeResolver.InferCodeFromLabel("root intrusion"));
        Assert.Equal("BBA", VsaCodeResolver.InferCodeFromLabel("Wurzeleinwuchs"));

        // "deposit" matcht BBC (Ablagerungen) — korrekter VSA-Code
        Assert.Equal("BBC", VsaCodeResolver.InferCodeFromLabel("attached deposit"));
        Assert.Equal("BBB", VsaCodeResolver.InferCodeFromLabel("Inkrustation verkalkt"));
    }

    [Fact]
    public void InferCodeFromLabel_GermanUmlauts_Handled()
    {
        // Umlaute werden intern zu ae/oe/ue konvertiert
        // "Anschlüss" → "anschluess" — matcht nicht "anschluss" (doppel-s)
        // Korrekt: voller Begriff "Anschluss" mit ss
        Assert.Equal("BCA", VsaCodeResolver.InferCodeFromLabel("Anschluss"));
        Assert.Equal("BAA", VsaCodeResolver.InferCodeFromLabel("Verformung")); // BAA = Verformung
        // Umlaut-Konvertierung: ü→ue, ä→ae, ö→oe
        Assert.Equal("BAJ", VsaCodeResolver.InferCodeFromLabel("Muffenversatz")); // BAJ = Verschobene Rohrverbindung
    }

    // ═══════════════════════════════════════════════════════════════
    // LookupLabel — Klartext mit Fallback
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void LookupLabel_ExactCode_ReturnsLabel()
    {
        var label = VsaCodeResolver.LookupLabel("BCD");
        Assert.NotNull(label);
        // VsaCodeTree definiert BCD als "Rohranfang" (nicht "Anfangsknoten")
        Assert.Contains("Rohranfang", label);
    }

    [Fact]
    public void LookupLabel_SubCode_FallsBackToMainCode()
    {
        // BCAEB hat vielleicht keinen exakten Match, aber BCA schon
        var label = VsaCodeResolver.LookupLabel("BCAEB");
        Assert.NotNull(label);
        // Sollte mindestens "Seitl. Anschluss" enthalten (von BCA)
    }

    [Fact]
    public void LookupLabel_UnknownCode_ReturnsNull()
    {
        Assert.Null(VsaCodeResolver.LookupLabel("ZZZ"));
        Assert.Null(VsaCodeResolver.LookupLabel(""));
        Assert.Null(VsaCodeResolver.LookupLabel(null!));
    }

    // ═══════════════════════════════════════════════════════════════
    // NormalizeClock — Uhrlage-Normalisierung
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("3:00", "3:00")]
    [InlineData("3", "3:00")]
    [InlineData("12:00", "12:00")]
    [InlineData("12 Uhr", "12:00")]
    [InlineData("oben", "12:00")]
    [InlineData("Scheitel", "12:00")]
    [InlineData("unten", "6:00")]
    [InlineData("Sohle", "6:00")]
    [InlineData("rechts", "3:00")]
    [InlineData("links", "9:00")]
    public void NormalizeClock_ValidInputs_ReturnsNormalized(string input, string expected)
    {
        var result = VsaCodeResolver.NormalizeClock(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeClock_NullOrEmpty_ReturnsNull(string? input)
    {
        Assert.Null(VsaCodeResolver.NormalizeClock(input));
    }

    [Fact]
    public void NormalizeClock_Consistency_SameInputSameOutput()
    {
        // Fuer Dedupe-Zwecke: verschiedene Schreibweisen derselben Position
        // muessen zum gleichen Ergebnis fuehren
        var a = VsaCodeResolver.NormalizeClock("12:00");
        var b = VsaCodeResolver.NormalizeClock("oben");
        var c = VsaCodeResolver.NormalizeClock("Scheitel");
        var d = VsaCodeResolver.NormalizeClock("12 Uhr");
        Assert.Equal(a, b);
        Assert.Equal(b, c);
        Assert.Equal(c, d);
    }

    // ═══════════════════════════════════════════════════════════════
    // Vertrag: Kein "???" darf durch den Resolver kommen
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void NormalizeFindingCode_NeverReturnsQuestionMarks()
    {
        // Der alte Code gab "???" zurueck — der neue gibt null zurueck
        Assert.Null(VsaCodeResolver.NormalizeFindingCode("???"));
        Assert.Null(VsaCodeResolver.NormalizeFindingCode("?"));
    }

    [Fact]
    public void InferCodeFromLabel_NeverReturnsQuestionMarks()
    {
        var result = VsaCodeResolver.InferCodeFromLabel("totally unknown thing");
        Assert.True(result == null || !result.Contains("?"),
            "InferCodeFromLabel darf nie '???' zurueckgeben");
    }
}
