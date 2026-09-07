using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Nova-Etappe 2b, Task 2 (Inventar 4.3, Zellregeln): Zeilenstatus-Regel, WPF-frei.</summary>
public sealed class HaltungZeilenStatusTests
{
    private static HaltungRecord Haltung(
        string status = "",
        string link = "",
        string zustandsklasse = "",
        string pdfPath = "",
        string pdfEigen = "",
        string pdfAll = "",
        int offeneKiBefunde = 0)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, "1-2", FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.WorkflowStatus, status, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.ConditionClass, zustandsklasse, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.PdfPath, pdfPath, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.PdfEigen, pdfEigen, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.PdfAll, pdfAll, FieldSource.Manual, false);

        if (offeneKiBefunde > 0)
        {
            r.Protocol = new ProtocolDocument();
            r.Protocol.Current ??= new ProtocolRevision();
            for (var i = 0; i < offeneKiBefunde; i++)
                r.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Ai = new ProtocolEntryAiMeta { Accepted = false, Confidence = 0.9 } });
        }

        return r;
    }

    [Fact]
    public void Leerer_Datensatz_hat_keine_Analyse_und_keine_Zustandsklasse()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung());

        Assert.Equal(KiAmpel.KeineAnalyse, ergebnis.Ampel);
        Assert.Equal("keine Analyse", ergebnis.AmpelText);
        Assert.Equal(0, ergebnis.OffeneBefunde);
        Assert.Equal(HaltungPruefstand.Offen, ergebnis.Pruefstand);
        Assert.False(ergebnis.HatVideo);
        Assert.False(ergebnis.HatProtokoll);
        Assert.Equal("–", ergebnis.ZustandsklasseChip);
    }

    [Fact]
    public void Zwei_offene_KI_Befunde_zeigen_die_Anzahl()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung(offeneKiBefunde: 2));

        Assert.Equal(KiAmpel.Offen, ergebnis.Ampel);
        Assert.Equal("2 offen", ergebnis.AmpelText);
        Assert.Equal(2, ergebnis.OffeneBefunde);
    }

    [Fact]
    public void Offene_KI_Befunde_gelten_auch_an_einer_abgeschlossenen_Haltung()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen", zustandsklasse: "4", offeneKiBefunde: 3));

        Assert.Equal(HaltungPruefstand.Abgeschlossen, ergebnis.Pruefstand);
        Assert.Equal(KiAmpel.Offen, ergebnis.Ampel);
        Assert.Equal("3 offen", ergebnis.AmpelText);
    }

    [Fact]
    public void Abgeschlossen_und_Z1_ist_kritisch()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen", zustandsklasse: "1"));

        Assert.Equal(KiAmpel.Kritisch, ergebnis.Ampel);
        Assert.Equal("geprüft", ergebnis.AmpelText);
        Assert.Equal(0, ergebnis.OffeneBefunde);
        Assert.Equal("Z1", ergebnis.ZustandsklasseChip);
    }

    [Fact]
    public void Abgeschlossen_und_Z4_ist_geprueft_nicht_kritisch()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen", zustandsklasse: "4"));

        Assert.Equal(KiAmpel.Geprueft, ergebnis.Ampel);
        Assert.Equal("geprüft", ergebnis.AmpelText);
        Assert.Equal("Z4", ergebnis.ZustandsklasseChip);
    }

    [Fact]
    public void Zustandsklasse_0_ist_ebenfalls_kritisch()
        => Assert.Equal(KiAmpel.Kritisch, HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen", zustandsklasse: "0")).Ampel);

    [Fact]
    public void PruefungText_stammt_aus_HaltungPruefstatus()
    {
        var ergebnis = HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen"));
        Assert.Equal(HaltungPruefstatus.Text(HaltungPruefstand.Abgeschlossen), ergebnis.PruefungText);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("video.mp4", true)]
    public void HatVideo_folgt_dem_Link_Feld(string link, bool erwartet)
        => Assert.Equal(erwartet, HaltungZeilenStatus.Bestimme(Haltung(link: link)).HatVideo);

    [Fact]
    public void HatProtokoll_ist_wahr_wenn_irgendeines_der_drei_PDF_Felder_gefuellt_ist()
    {
        Assert.False(HaltungZeilenStatus.Bestimme(Haltung()).HatProtokoll);
        Assert.True(HaltungZeilenStatus.Bestimme(Haltung(pdfPath: "a.pdf")).HatProtokoll);
        Assert.True(HaltungZeilenStatus.Bestimme(Haltung(pdfEigen: "b.pdf")).HatProtokoll);
        Assert.True(HaltungZeilenStatus.Bestimme(Haltung(pdfAll: "c.pdf")).HatProtokoll);
    }

    [Theory]
    [InlineData("5", "–")]
    [InlineData("-1", "–")]
    [InlineData("abc", "–")]
    [InlineData("0", "Z0")]
    [InlineData("2", "Z2")]
    public void ZustandsklasseChip_zeigt_Z0_bis_Z4_sonst_Strich(string wert, string erwartet)
        => Assert.Equal(erwartet, HaltungZeilenStatus.Bestimme(Haltung(status: "abgeschlossen", zustandsklasse: wert)).ZustandsklasseChip);
}
