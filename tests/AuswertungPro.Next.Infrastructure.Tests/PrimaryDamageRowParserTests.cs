using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer PrimaryDamageRowParser –
/// dokumentieren das IST-Verhalten vor und nach der Extraktion.
/// </summary>
public sealed class PrimaryDamageRowParserTests
{
    // ─── TryParseDamageRow ───────────────────────────────────────────────────

    [Fact]
    public void TryParseDamageRow_StandardFormat_ExtraktiertDistCodeDesc()
    {
        // Standardformat: "[meter] [code] [beschreibung]"
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "  10.50  BAB  Laengsriss",
            out var dist, out var code, out var desc);

        Assert.True(result);
        Assert.Equal("10.50", dist);
        Assert.Equal("BAB", code);
        Assert.Equal("Laengsriss", desc);
    }

    [Fact]
    public void TryParseDamageRow_StandardFormatZweiCodes_VerbindetMitLeerzeichen()
    {
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "  5.30  BAB A  Querriss",
            out var dist, out var code, out var desc);

        Assert.True(result);
        Assert.Equal("5.30", dist);
        Assert.Equal("BAB A", code);
        Assert.Equal("Querriss", desc);
    }

    [Fact]
    public void TryParseDamageRow_FretzFormat_ExtraktiertDistCodeDesc()
    {
        // Fretz-Format: "[HH:MM:SS] [meter] [code] [beschreibung]"
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "00:01:31  4.60  BCC.Y.B  Bogen",
            out var dist, out var code, out var desc);

        Assert.True(result);
        Assert.Equal("4.60", dist);
        Assert.Equal("BCC.Y.B", code);
        Assert.Equal("Bogen", desc);
    }

    [Fact]
    public void TryParseDamageRow_FretzFormatMitKomma_NormaliziertPunkt()
    {
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "00:02:10  12,30  BAF  Korrosion",
            out var dist, out var code, out var desc);

        Assert.True(result);
        Assert.Equal("12.30", dist);
    }

    [Fact]
    public void TryParseDamageRow_LeereZeile_GibtFalseZurueck()
    {
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "",
            out var dist, out var code, out var desc);

        Assert.False(result);
        Assert.Equal("", dist);
        Assert.Equal("", code);
        Assert.Equal("", desc);
    }

    [Fact]
    public void TryParseDamageRow_NurText_GibtFalseZurueck()
    {
        var result = PrimaryDamageRowParser.TryParseDamageRow(
            "Seite 1 von 3",
            out _, out _, out _);

        Assert.False(result);
    }

    // ─── TakeFirstColumn ─────────────────────────────────────────────────────

    [Fact]
    public void TakeFirstColumn_MehrereSpaltendurchDoppelLeerzeichen_GibtErsteSpaltezurueck()
    {
        var result = PrimaryDamageRowParser.TakeFirstColumn("Riss  34  1");

        Assert.Equal("Riss", result);
    }

    [Fact]
    public void TakeFirstColumn_EinzelnesSpalte_GibtGesamtTextZurueck()
    {
        var result = PrimaryDamageRowParser.TakeFirstColumn("NurEinSpaltenText");

        Assert.Equal("NurEinSpaltenText", result);
    }

    [Fact]
    public void TakeFirstColumn_NullSafeErsatzLeererString_GibtLeerStringZurueck()
    {
        var result = PrimaryDamageRowParser.TakeFirstColumn(null!);

        Assert.Equal("", result);
    }

    // ─── StripTrailingNoise ──────────────────────────────────────────────────

    [Fact]
    public void StripTrailingNoise_TimestampAmEnde_WirdEntfernt()
    {
        var result = PrimaryDamageRowParser.StripTrailingNoise("Laengsriss 00:01:45");

        Assert.Equal("Laengsriss", result);
    }

    [Fact]
    public void StripTrailingNoise_KeinTimestamp_UnveraendertZurueck()
    {
        var result = PrimaryDamageRowParser.StripTrailingNoise("Laengsriss");

        Assert.Equal("Laengsriss", result);
    }

    [Fact]
    public void StripTrailingNoise_NullSafeErsatz_GibtLeerStringZurueck()
    {
        var result = PrimaryDamageRowParser.StripTrailingNoise(null!);

        Assert.Equal("", result);
    }

    // ─── IsNoiseLine ─────────────────────────────────────────────────────────

    [Fact]
    public void IsNoiseLine_SeitenAngabe_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("Seite 1"));
    }

    [Fact]
    public void IsNoiseLine_PageAngabeEnglisch_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("Page 3"));
    }

    [Fact]
    public void IsNoiseLine_LangeZahl_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("12345678"));
    }

    [Fact]
    public void IsNoiseLine_BilddateiJpg_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("bild.jpg"));
    }

    [Fact]
    public void IsNoiseLine_GuidZeile_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("a1b2c3d4-e5f6-7890-abcd-ef0123456789"));
    }

    [Fact]
    public void IsNoiseLine_TimestampAllein_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine("00:01:31"));
    }

    [Fact]
    public void IsNoiseLine_TimestampMitMeterwertUndCode_IstKeinNoise()
    {
        // Fretz-Format: Echte Beobachtungszeile mit Timestamp + Meterwert + VSA-Code
        Assert.False(PrimaryDamageRowParser.IsNoiseLine("00:01:31 4.60 BCC.Y.B Bogen"));
    }

    [Fact]
    public void IsNoiseLine_NormalerSchadenstext_IstKeinNoise()
    {
        Assert.False(PrimaryDamageRowParser.IsNoiseLine("Laengsriss im Scheitel"));
    }

    [Fact]
    public void IsNoiseLine_LeerString_IstNoise()
    {
        Assert.True(PrimaryDamageRowParser.IsNoiseLine(""));
    }

    // ─── ExtractPrimaryDamages (Integrations-Ebene) ──────────────────────────

    [Fact]
    public void ExtractPrimaryDamages_StandardFormat_ErstelltEintraege()
    {
        var lines = new[]
        {
            "10.50  BAB  Laengsriss",
            "25.00  BBC  Ablagerung"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        Assert.Contains("BAB @10.50m", result);
        Assert.Contains("BBC @25.00m", result);
    }

    [Fact]
    public void ExtractPrimaryDamages_FretzFormat_ErstelltEintraege()
    {
        var lines = new[]
        {
            "00:01:31  4.60  BCC.Y.B  Bogen",
            "00:02:10  12.30  BAF  Korrosion"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        Assert.Contains("BCC.Y.B @4.60m", result);
        Assert.Contains("BAF @12.30m", result);
    }

    [Fact]
    public void ExtractPrimaryDamages_LeerzeileTrenntEintraege_FlushesKorrekt()
    {
        var lines = new[]
        {
            "10.50  BAB  Laengsriss",
            "",
            "25.00  BBC  Ablagerung"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        Assert.Contains("BAB @10.50m", result);
        Assert.Contains("BBC @25.00m", result);
    }

    [Fact]
    public void ExtractPrimaryDamages_KeineSchaeden_GibtLeerStringZurueck()
    {
        var lines = new[]
        {
            "Seite 1 von 3",
            "Kanalfernsehprotokoll"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractPrimaryDamages_NoiseLinesIgnoriert()
    {
        var lines = new[]
        {
            "10.50  BAB  Laengsriss",
            "Seite 1",
            "12345678"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        // Nur BAB-Eintrag, keine Noise-Zeilen im Ergebnis
        var entries = result.Split('\n');
        Assert.Single(entries);
        Assert.Contains("BAB", entries[0]);
    }

    [Fact]
    public void ExtractPrimaryDamages_FortsetzungszeileAngehaengt()
    {
        var lines = new[]
        {
            "10.50  BAB  Riss",
            "Fortsetzung der Beschreibung"
        };

        var result = PrimaryDamageRowParser.ExtractPrimaryDamages(lines);

        Assert.Contains("Fortsetzung der Beschreibung", result);
    }
}
