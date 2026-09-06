using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 3 des Uebergabeplans vom 2026-09-05: Eine alte VSA-XTF muss ihre
/// Haltungen UND ihre Schaechte richtig einlesen.
///
/// Vorher (Stand c1021e76e) erzeugte der Leser aus JEDER Untersuchung einen
/// Haltungsdatensatz und las <c>Normschachtschaden</c> ueberhaupt nicht. Andermatt
/// Zone 2.11 ergab dadurch 47 Haltungen und null Schaechte — Schacht 3133 stand mit
/// seinen acht Schachtschaeden in der Haltungsliste.
/// </summary>
public sealed class AlteVsaKekSchachtImportTests
{
    [Fact]
    public void Schachtbegehung_WirdSchachtUndNichtHaltung()
    {
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "327015-2414", "327015", "2414", AnzahlKanalschaeden: 7)],
                schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 8)]),
            (projekt, stats) =>
            {
                Assert.Equal(0, stats.Errors);

                var haltung = Assert.Single(projekt.Data);
                Assert.Equal("327015-2414", haltung.GetFieldValue("Haltungsname"));

                var schacht = Assert.Single(projekt.SchaechteData);
                Assert.Equal("3133", schacht.GetFieldValue("Schachtnummer"));

                var eintraege = schacht.Protocol?.Current?.Entries ?? [];
                Assert.Equal(8, eintraege.Count);
                Assert.All(eintraege, e => Assert.StartsWith("DA", e.Code, StringComparison.Ordinal));
            });
    }

    [Fact]
    public void SchachtOhneSchaeden_BleibtTrotzdemEinSchacht()
    {
        // "Keine Schaeden" heisst nicht "keine Schachtbegehung". Die Erfassungsart
        // Begehung ist der Beleg.
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3060", AnzahlSchachtschaeden: 0)]),
            (projekt, stats) =>
            {
                Assert.Empty(projekt.Data);
                var schacht = Assert.Single(projekt.SchaechteData);
                Assert.Equal("3060", schacht.GetFieldValue("Schachtnummer"));
            });
    }

    [Fact]
    public void UnklareUntersuchung_WirdWederHaltungNochSchacht()
    {
        // Der reale Fall "2204" aus Zone 2.11: keine Erfassungsart, keine Punkte,
        // keine Schaeden, Platzhalterdatum. Raten waere hier ein Fehler.
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                unklare: [new AlteVsaKekTestquelle.UnklareUntersuchung("chX1", "2204")]),
            (projekt, stats) =>
            {
                Assert.Empty(projekt.Data);
                Assert.Empty(projekt.SchaechteData);
                Assert.Contains(stats.Messages,
                    m => m.Message.Contains("2204", StringComparison.Ordinal)
                         && m.Level == "Warn");
            });
    }

    [Fact]
    public void ZweiBegehungenDesselbenSchachts_ErgebenEinenSchacht()
    {
        // Schacht 2200 aus Zone 2.11 wurde zweimal begangen. Das ist EIN Bauwerk.
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(schaechte:
            [
                new AlteVsaKekTestquelle.Schacht("chS1", "2200", AnzahlSchachtschaeden: 11, Zeitpunkt: "20200703"),
                new AlteVsaKekTestquelle.Schacht("chS2", "2200", AnzahlSchachtschaeden: 5, Zeitpunkt: "20200710")
            ]),
            (projekt, stats) =>
            {
                Assert.Empty(projekt.Data);
                var schacht = Assert.Single(projekt.SchaechteData);

                // Nichts geht verloren: Die erste Begehung ist die Arbeitskopie, die
                // zweite liegt als Revision daneben und wird gemeldet.
                Assert.Equal(11, schacht.Protocol?.Current?.Entries.Count);
                var revision = Assert.Single(schacht.Protocol?.History ?? []);
                Assert.Equal(5, revision.Entries.Count);
                Assert.Contains(stats.Messages,
                    m => m.Level == "Warn"
                         && m.Message.Contains("2200", StringComparison.Ordinal)
                         && m.Message.Contains("weitere Begehung", StringComparison.Ordinal));
            });
    }

    [Fact]
    public void VorhandenesSchachtprotokoll_WirdNieUeberschrieben()
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"ap3-bestand-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, "Zone.xtf");
        File.WriteAllText(datei, AlteVsaKekTestquelle.BaueXml(
            schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 8)]));

        try
        {
            var projekt = new Project();
            var vorhanden = new SchachtRecord();
            vorhanden.SetFieldValue("Schachtnummer", "3133");
            vorhanden.Protocol = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
            {
                HaltungId = "3133",
                Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Comment = "Von Hand erfasst",
                    Entries = [new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "DAAA" }]
                }
            };
            projekt.SchaechteData.Add(vorhanden);

            new LegacyXtfImportService().ImportXtfFiles([datei], projekt);

            Assert.Single(projekt.SchaechteData);
            var eintrag = Assert.Single(vorhanden.Protocol!.Current!.Entries);
            Assert.Equal("DAAA", eintrag.Code);
            Assert.Equal(8, Assert.Single(vorhanden.Protocol.History ?? []).Entries.Count);
        }
        finally
        {
            try { Directory.Delete(ordner, recursive: true); } catch { }
        }
    }

    [Fact]
    public void SchachtDatumUndOperateur_KommenAn()
    {
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 2, Operateur: "Pascal Aschwanden")]),
            (projekt, stats) =>
            {
                var schacht = Assert.Single(projekt.SchaechteData);
                Assert.Contains("2020", schacht.GetFieldValue("Datum_Jahr") ?? "", StringComparison.Ordinal);
                Assert.Contains("Pascal Aschwanden",
                    string.Join(" ", schacht.Fields.Values), StringComparison.Ordinal);
            });
    }

    [Fact]
    public void VerwaisterSchadensverweis_StopptDenImportNicht()
    {
        var xml = AlteVsaKekTestquelle.BaueXml(
            haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "100-200", "100", "200", AnzahlKanalschaeden: 2)]);
        // Ein Schachtschaden, dessen Untersuchung es nicht gibt.
        xml = xml.Replace("    </VSA_KEK.KEK>", """
      <VSA_KEK.KEK.Normschachtschaden TID="chNSwaise">
        <UntersuchungRef REF="gibtEsNicht" />
        <SchachtSchadencode>DAF</SchachtSchadencode>
      </VSA_KEK.KEK.Normschachtschaden>
    </VSA_KEK.KEK>
""");

        MitDatei(xml, (projekt, stats) =>
        {
            Assert.Equal(0, stats.Errors);
            Assert.Single(projekt.Data);
            Assert.Empty(projekt.SchaechteData);
        });
    }

    [Fact]
    public void WiderspruechlicheUntersuchung_BleibtOffen()
    {
        // Von-/bisPunkt (Haltung) UND Normschachtschaeden (Schacht) am selben Objekt.
        var xml = AlteVsaKekTestquelle.BaueXml(
            haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "100-200", "100", "200", AnzahlKanalschaeden: 0)]);
        xml = xml.Replace("    </VSA_KEK.KEK>", """
      <VSA_KEK.KEK.Normschachtschaden TID="chNS1">
        <UntersuchungRef REF="chU1" />
        <SchachtSchadencode>DAF</SchachtSchadencode>
      </VSA_KEK.KEK.Normschachtschaden>
    </VSA_KEK.KEK>
""");

        MitDatei(xml, (projekt, stats) =>
        {
            Assert.Empty(projekt.Data);
            Assert.Empty(projekt.SchaechteData);
            Assert.Contains(stats.Messages,
                m => m.Level == "Warn" && m.Message.Contains("100-200", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void UntersuchungOhneJedenBeleg_AberMitHaltungsname_BleibtHaltung()
    {
        // Der IKAS-Fall: nur Bezeichnung, Zeitpunkt und eine Videodatei. Bis 2026-09-05
        // wurde daraus immer eine Haltung — das darf die neue Regel nicht wegnehmen.
        MitDatei("""
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""", (projekt, stats) =>
        {
            var haltung = Assert.Single(projekt.Data);
            Assert.Equal("06-001", haltung.GetFieldValue("Haltungsname"));
            Assert.Empty(projekt.SchaechteData);
        });
    }

    [Fact]
    public void EinzelneNummerOhneBeleg_WirdNichtZumSchachtGeraten()
    {
        // Die Umkehrung gilt ausdruecklich NICHT: Eine einzelne Nummer ist kein Beleg.
        MitDatei("""
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>2204</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""", (projekt, stats) =>
        {
            Assert.Empty(projekt.Data);
            Assert.Empty(projekt.SchaechteData);
        });
    }

    [Fact]
    public void Bauwerksverweis_SchlaegtDenNamensrueckfall()
    {
        // Liegt der Normschacht in derselben Datei, gilt er — auch wenn der Name
        // wie eine Haltung aussieht.
        MitDatei("""
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK" VERSION="15.07.2008" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK.KEK BID="B1">
      <VSA_KEK.KEK.Untersuchung TID="U1">
        <Bezeichnung>12-4</Bezeichnung>
        <AbwasserbauwerkRef REF="N1" />
      </VSA_KEK.KEK.Untersuchung>
    </VSA_KEK.KEK>
    <SIA405_Abwasser.Abwasser BID="B2">
      <SIA405_Abwasser.Abwasser.Normschacht TID="N1"><Bezeichnung>12-4</Bezeichnung></SIA405_Abwasser.Abwasser.Normschacht>
    </SIA405_Abwasser.Abwasser>
  </DATASECTION>
</TRANSFER>
""", (projekt, stats) =>
        {
            Assert.Empty(projekt.Data);
            var schacht = Assert.Single(projekt.SchaechteData);
            Assert.Equal("12-4", schacht.GetFieldValue("Schachtnummer"));
        });
    }

    [Fact]
    public void SchachtbegehungBerechnetKeineZustandsklasse()
    {
        // Entscheid Pascal, 2026-09-05: Die Zustandsklasse eines Schachts wird NICHT aus
        // den Schachtschaeden abgeleitet — das macht die Fachperson von Hand.
        //
        // Der Unterschied zur Haltung ist beabsichtigt: Dort rechnet der
        // VsaEvaluationService aus den Befunden. Am Schacht darf ein Import allenfalls
        // einen Wert uebernehmen, den die Quelle ausdruecklich nennt (SIA405
        // "BaulicherZustand"), aber niemals selbst einen errechnen.
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 8)]),
            (projekt, stats) =>
            {
                var schacht = Assert.Single(projekt.SchaechteData);
                Assert.Equal(8, schacht.Protocol?.Current?.Entries.Count);
                Assert.True(
                    string.IsNullOrWhiteSpace(schacht.GetFieldValue("Zustandsklasse")),
                    "Die Zustandsklasse eines Schachts wird von Hand gesetzt, nie aus den Schaeden berechnet.");
            });
    }

    [Fact]
    public void BauwerkeUndUntersuchungen_WerdenGetrenntGezaehlt()
    {
        MitDatei(
            AlteVsaKekTestquelle.BaueXml(
                haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "100-200", "100", "200", AnzahlKanalschaeden: 1)],
                schaechte:
                [
                    new AlteVsaKekTestquelle.Schacht("chS1", "100", AnzahlSchachtschaeden: 3),
                    new AlteVsaKekTestquelle.Schacht("chS2", "200", AnzahlSchachtschaeden: 2)
                ],
                unklare: [new AlteVsaKekTestquelle.UnklareUntersuchung("chX1", "2204")]),
            (projekt, stats) =>
            {
                var text = string.Join("\n", stats.Messages.Select(m => m.Message));
                // Vier Untersuchungen, aber nur drei Bauwerke — und ein ungeklaerter Fall.
                Assert.Contains("4 Untersuchungen", text, StringComparison.Ordinal);
                Assert.Contains("1 Haltung", text, StringComparison.Ordinal);
                Assert.Contains("2 Schaecht", text, StringComparison.Ordinal);
                Assert.Contains("1 ungeklaert", text, StringComparison.Ordinal);
            });
    }

    // ---------------------------------------------------------------------

    private static void MitDatei(
        string xml,
        Action<Project, AuswertungPro.Next.Infrastructure.Import.Common.ImportStats> pruefung)
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"ap3-vsakek-{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, "Zone.xtf");
        File.WriteAllText(datei, xml);

        try
        {
            var projekt = new Project();
            var stats = new LegacyXtfImportService().ImportXtfFiles([datei], projekt);
            pruefung(projekt, stats);
        }
        finally
        {
            try { Directory.Delete(ordner, recursive: true); } catch { }
        }
    }
}
