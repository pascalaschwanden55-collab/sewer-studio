using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Arbeitspaket 10 des Uebergabeplans vom 2026-09-05: Die Kennungsherkunft entscheidet,
/// nicht die blosse Form.
///
/// Vorher (Stand c1021e76e) galt „sechzehn Zeichen, beginnt mit einem Buchstaben" als
/// Beleg fuer eine neuere Katasterquelle. Eine XTF-TID von WinCan sieht genauso aus
/// (<c>ch2585eaef000001</c>) und blockierte damit einen geprueften Katastertreffer.
/// </summary>
public sealed class KatasterKennungHerkunftTests
{
    // ---------------------------------------------------------------------
    // Die reine Regel
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("ch2585eaef000001", FieldSource.Xtf, KennungsHerkunft.Dateikennung)]
    [InlineData("ch2585eaef000001", FieldSource.Xtf405, KennungsHerkunft.Dateikennung)]
    [InlineData("chSSTa1b2c3d4e5f", FieldSource.Xtf, KennungsHerkunft.LokaleExportkennung)]
    [InlineData("ch23h1a4000000ab", FieldSource.Kataster, KennungsHerkunft.BestaetigtesGeonis)]
    [InlineData("ch23h1a4000000ab", FieldSource.Legacy, KennungsHerkunft.Unbekannt)]
    [InlineData("866789", FieldSource.Kataster, KennungsHerkunft.Keine)]
    [InlineData("", FieldSource.Kataster, KennungsHerkunft.Keine)]
    public void HerkunftFolgtDerFeldquelle(string kennung, FieldSource quelle, KennungsHerkunft erwartet)
        => Assert.Equal(erwartet, KatasterKennungHerkunft.Bestimme(kennung, quelle, handgesetzt: false));

    [Fact]
    public void EineHandeingabe_GiltAlsBestaetigt()
        => Assert.Equal(
            KennungsHerkunft.BestaetigtesGeonis,
            KatasterKennungHerkunft.Bestimme("ch23h1a4000000ab", FieldSource.Legacy, handgesetzt: true));

    [Fact]
    public void EigeneExportkennung_BlockiertNieUndAuchNichtVonHand()
    {
        // Sie beschreibt kein Katasterobjekt — auch nicht, wenn jemand sie bestaetigt hat.
        Assert.Equal(
            KennungsHerkunft.LokaleExportkennung,
            KatasterKennungHerkunft.Bestimme("chSSTa1b2c3d4e5f", FieldSource.Manual, handgesetzt: true));
        Assert.False(KatasterKennungHerkunft.BlockiertUebernahme(KennungsHerkunft.LokaleExportkennung));
    }

    [Theory]
    [InlineData(KennungsHerkunft.BestaetigtesGeonis, true)]
    [InlineData(KennungsHerkunft.Unbekannt, true)]
    [InlineData(KennungsHerkunft.Dateikennung, true)]
    [InlineData(KennungsHerkunft.LokaleExportkennung, false)]
    [InlineData(KennungsHerkunft.Keine, false)]
    public void NurBelegteKennungenBlockieren(KennungsHerkunft herkunft, bool blockiert)
        => Assert.Equal(blockiert, KatasterKennungHerkunft.BlockiertUebernahme(herkunft));

    // ---------------------------------------------------------------------
    // Wirkung im Plan
    // ---------------------------------------------------------------------

    [Fact]
    public void XtfTidOhneHerkunftsbeleg_LaesstWiderspruchOffen()
    {
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("1000-2000", "ch2585eaef000001", FieldSource.Xtf)],
            Bestand("1000-2000"));

        Assert.Empty(plan.Positionen);
        Assert.Equal(KatasterKennungGrund.HerkunftUnklar, Assert.Single(plan.Hinweise).Grund);
    }

    [Fact]
    public void EigeneExportkennung_BlockiertDenKatastertrefferNicht()
    {
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("1000-2000", "chSSTa1b2c3d4e5f", FieldSource.Xtf)],
            Bestand("1000-2000"));

        Assert.Single(plan.Positionen);
        Assert.Empty(plan.Hinweise);
    }

    [Fact]
    public void BestaetigteFremdeKennung_BleibtGeschuetzt()
    {
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("1000-2000", "ch23h1a4000000ab", FieldSource.Kataster)],
            Bestand("1000-2000"));

        Assert.Empty(plan.Positionen);
        var hinweis = Assert.Single(plan.Hinweise);
        Assert.Equal(KatasterKennungGrund.Abweichend, hinweis.Grund);
    }

    [Fact]
    public void UnbekannteHerkunft_WirdZumPrueffallStattStillZuGewinnen()
    {
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("1000-2000", "ch23h1a4000000ab", FieldSource.Legacy)],
            Bestand("1000-2000"));

        Assert.Empty(plan.Positionen);
        var hinweis = Assert.Single(plan.Hinweise);
        Assert.Equal(KatasterKennungGrund.HerkunftUnklar, hinweis.Grund);
    }

    [Fact]
    public void GleicheKennung_BleibtBereitsVorhanden()
    {
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("1000-2000", "chKATASTER000001", FieldSource.Kataster)],
            Bestand("1000-2000", hauptkennung: "chKATASTER000001"));

        // Gleiche Kennung: keine Blockade, die Verbundkennungen duerfen nachgezogen werden.
        Assert.Single(plan.Positionen);
    }

    [Fact]
    public void SchaechteFolgenDerselbenRegel()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "3133");
        schacht.SetFieldValue(FieldKeys.CadastreObjectId, "ch2585eaef000001", FieldSource.Xtf, userEdited: false);

        var plan = KatasterKennungPlanBuilder.BaueFuerSchaechte([schacht], SchachtBestand("3133"));

        Assert.Empty(plan.Positionen);
        Assert.Equal(KatasterKennungGrund.HerkunftUnklar, Assert.Single(plan.Hinweise).Grund);
    }

    // ---------------------------------------------------------------------

    private static HaltungRecord Haltung(string name, string objektId, FieldSource quelle)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Legacy, userEdited: false);
        record.SetFieldValue(FieldKeys.CadastreObjectId, objektId, quelle, userEdited: false);
        return record;
    }

    private static KatasterKennungBestand Bestand(string name, string hauptkennung = "chKATASTER000001")
        => new(
            BauteilArt.Haltung,
            new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase)
            {
                [name] = KatasterKennung.FuerHaltung(
                    name, "Altdorf", hauptkennung, "chKANAL00000001",
                    null, null, null, null, null, null)
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            1,
            "Dezember 2024");

    private static KatasterKennungBestand SchachtBestand(string name)
        => new(
            BauteilArt.Schacht,
            new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase)
            {
                [name] = KatasterKennung.FuerSchacht(name, "Altdorf", "chKNOTEN00000001", "chBAUWERK0000001")
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            1,
            "Dezember 2024");
}
