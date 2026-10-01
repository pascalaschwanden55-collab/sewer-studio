using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 2 des Uebergabeplans vom 2026-09-05: Quellen vollstaendig erfassen und
/// begruendet auswaehlen.
///
/// Vorher (Stand c1021e76e) suchte die Erkennung in den ersten 64 KiB Text nach der
/// Zeichenfolge <c>VSA_KEK_2020_LV95</c>. Damit fielen alle aelteren WinCan-Exporte
/// durch, die SIA-Abhaengigkeit im selben Kopf verdeckte die Untersuchungen, und ein
/// langer Kopf konnte den Modellnamen ganz aus dem Fenster schieben.
/// </summary>
public sealed class XtfQuellenerkennungTests
{
    // ---------------------------------------------------------------------
    // Pruefer: Modellfamilie und Fachinhalt
    // ---------------------------------------------------------------------

    [Fact]
    public void AlteVsaKekDatei_WirdAlsInspektionsquelleErkannt()
    {
        WithDatei(
            AlteVsaKekTestquelle.BaueXml(
                haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "100-200", "100", "200", AnzahlKanalschaeden: 3)],
                schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 8)]),
            pfad =>
            {
                var merkmale = new XtfQuellenPruefer().Pruefe(pfad);

                Assert.Null(merkmale.Lesefehler);
                Assert.Equal(2, merkmale.Untersuchungen);
                Assert.Equal(3, merkmale.Kanalschaeden);
                Assert.Equal(8, merkmale.Normschachtschaeden);
                // Der Kopf fuehrt VSA_KEK UND SIA405 — beides muss erkannt werden.
                Assert.Equal(XtfModellfamilie.VsaKekUndSia405, XtfQuellenklassifikation.Familie(merkmale));
                // Entscheidend ist der Inhalt, nicht der Kopf: Das ist eine Inspektionsdatei.
                Assert.Equal(XtfQuellenart.Inspektion, XtfQuellenklassifikation.Art(merkmale));
                Assert.True(XtfQuellenklassifikation.IstInspektionsquelle(merkmale));
            });
    }

    [Fact]
    public void ReineKatasterdatei_GiltNichtAlsInspektionsquelle()
    {
        WithDatei("""
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="Test">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" VERSION="26.06.2021" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung BID="B1">
      <SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Kanal TID="K1"><Bezeichnung>100-200</Bezeichnung></SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Kanal>
      <SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Haltung TID="H1"><Bezeichnung>100-200</Bezeichnung></SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Haltung>
      <SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Normschacht TID="N1"><Bezeichnung>100</Bezeichnung></SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung.Normschacht>
    </SIA405_ABWASSER_2020_LV95.Siedlungsentwaesserung>
  </DATASECTION>
</TRANSFER>
""", pfad =>
        {
            var merkmale = new XtfQuellenPruefer().Pruefe(pfad);

            Assert.Equal(0, merkmale.Untersuchungen);
            Assert.Equal(3, merkmale.Stammdatenobjekte);
            Assert.Equal(XtfModellfamilie.Sia405, XtfQuellenklassifikation.Familie(merkmale));
            Assert.Equal(XtfQuellenart.Kataster, XtfQuellenklassifikation.Art(merkmale));
            Assert.False(XtfQuellenklassifikation.IstInspektionsquelle(merkmale));
        });
    }

    [Fact]
    public void LangerKopf_VerstecktDasModellNichtMehr()
    {
        // 300 Kommentarzeilen vor der HEADERSECTION schieben den Modellnamen weit ueber
        // die frueheren 64 KiB hinaus. Der stroemende Leser findet ihn trotzdem.
        var fueller = string.Concat(Enumerable.Repeat("  <!-- " + new string('x', 400) + " -->\n", 300));
        WithDatei($"""
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
{fueller}  <HEADERSECTION VERSION="2.3" SENDER="Test">
    <MODELS><MODEL NAME="VSA_KEK" VERSION="15.07.2008" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK.KEK BID="B1">
      <VSA_KEK.KEK.Untersuchung TID="U1"><Bezeichnung>100-200</Bezeichnung></VSA_KEK.KEK.Untersuchung>
    </VSA_KEK.KEK>
  </DATASECTION>
</TRANSFER>
""", pfad =>
        {
            var merkmale = new XtfQuellenPruefer().Pruefe(pfad);

            Assert.Contains("VSA_KEK", merkmale.Modellnamen);
            Assert.Equal(1, merkmale.Untersuchungen);
            Assert.Equal(XtfModellfamilie.VsaKek, XtfQuellenklassifikation.Familie(merkmale));
        });
    }

    [Fact]
    public void DefektesXml_MeldetLesefehlerUndWirftNicht()
    {
        WithDatei("<TRANSFER><kaputt", pfad =>
        {
            var merkmale = new XtfQuellenPruefer().Pruefe(pfad);

            Assert.NotNull(merkmale.Lesefehler);
            Assert.Equal(XtfQuellenart.Unbekannt, XtfQuellenklassifikation.Art(merkmale));
            Assert.Contains("nicht lesbar", XtfQuellenklassifikation.Begruendung(merkmale), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ZweiExporteMitNeuenTids_HabenDenselbenFingerabdruck()
    {
        // Gemessen am 2026-09-05 an Andermatt Zone 2.11: WinCan vergibt bei JEDEM Export
        // neue TIDs. Der Fingerabdruck darf sich davon nicht taeuschen lassen, sonst
        // gilt derselbe Bestand als drei verschiedene Zonen.
        var haltungA = new AlteVsaKekTestquelle.Haltung("chAAAAAAAAAAAA01", "100-200", "100", "200", AnzahlKanalschaeden: 2);
        var haltungB = haltungA with { Tid = "chZZZZZZZZZZZZ99" };

        string? ersterAbdruck = null;
        foreach (var haltung in new[] { haltungA, haltungB })
        {
            WithDatei(AlteVsaKekTestquelle.BaueXml(haltungen: [haltung]), pfad =>
            {
                var abdruck = new XtfQuellenPruefer().Pruefe(pfad).Untersuchungsfingerabdruck;
                Assert.False(string.IsNullOrEmpty(abdruck));
                if (ersterAbdruck is null)
                    ersterAbdruck = abdruck;
                else
                    Assert.Equal(ersterAbdruck, abdruck);
            });
        }
    }

    [Fact]
    public void ZweiBegehungenDesselbenSchachts_ZaehlenBeide()
    {
        // Der Schacht 2200 aus Zone 2.11 wurde zweimal begangen. Eine Menge (statt einer
        // Mehrfachmenge) wuerde die zweite Begehung verschlucken.
        WithDatei(
            AlteVsaKekTestquelle.BaueXml(schaechte:
            [
                new AlteVsaKekTestquelle.Schacht("chS1", "2200", AnzahlSchachtschaeden: 11),
                new AlteVsaKekTestquelle.Schacht("chS2", "2200", AnzahlSchachtschaeden: 11)
            ]),
            pfad =>
            {
                var merkmale = new XtfQuellenPruefer().Pruefe(pfad);
                Assert.Equal(2, merkmale.Untersuchungen);
                Assert.Equal(22, merkmale.Normschachtschaeden);
            });
    }

    // ---------------------------------------------------------------------
    // Auswahl mehrerer Exporte
    // ---------------------------------------------------------------------

    [Fact]
    public void DreiExporteDerselbenZone_ErgebenEinenGelesenenExport()
    {
        // Der reale Fall Andermatt Zone 2.11: dreimal dieselben Untersuchungen,
        // nur der erste Export fuehrt zusaetzlich die Medienverweise.
        var reich = Merkmale(untersuchungen: 48, kanal: 209, schacht: 132, dateien: 220, fingerabdruck: "ZONE");
        var arm1 = Merkmale(untersuchungen: 48, kanal: 209, schacht: 132, dateien: 0, fingerabdruck: "ZONE");
        var arm2 = Merkmale(untersuchungen: 48, kanal: 209, schacht: 132, dateien: 0, fingerabdruck: "ZONE");

        // Der Zählerstand allein genügt nicht. Hier liefert die Leseseite explizit
        // dieselben vollständigen Objektbelege; nur Medien kommen im reichen dazu.
        reich = reich with { Inhaltsbelege = new Dictionary<string, int> { ["FACHINHALT"] = 389, ["MEDIEN"] = 220 } };
        arm1 = arm1 with { Inhaltsbelege = new Dictionary<string, int> { ["FACHINHALT"] = 389 } };
        arm2 = arm2 with { Inhaltsbelege = arm1.Inhaltsbelege };

        var wahl = XtfExportAuswahl.Waehle([
            new XtfExportKandidat(@"C:\x\_Zone_2.11\Zone_2.11.xtf", arm2),
            new XtfExportKandidat(@"C:\x\Zone_2.11\Zone_2.11.xtf", reich),
            new XtfExportKandidat(@"C:\x\Zone_2.11_\Zone_2.11.xtf", arm1)
        ]);

        var uebernommen = Assert.Single(wahl.Uebernommen);
        Assert.EndsWith(@"Zone_2.11\Zone_2.11.xtf", uebernommen, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, wahl.Entscheide.Count(e => !e.Uebernommen));
        Assert.All(wahl.Entscheide.Where(e => !e.Uebernommen),
            e => Assert.Contains("nicht nochmals gelesen", e.Grund, StringComparison.Ordinal));
    }

    [Fact]
    public void VerschiedeneZonen_WerdenAlleGelesen()
    {
        var a = Merkmale(untersuchungen: 10, kanal: 20, schacht: 0, dateien: 0, fingerabdruck: "ZONE_A");
        var b = Merkmale(untersuchungen: 7, kanal: 9, schacht: 0, dateien: 0, fingerabdruck: "ZONE_B");

        var wahl = XtfExportAuswahl.Waehle([
            new XtfExportKandidat(@"C:\x\a.xtf", a),
            new XtfExportKandidat(@"C:\x\b.xtf", b)
        ]);

        Assert.Equal(2, wahl.Uebernommen.Count);
    }

    [Fact]
    public void GleicheZoneMitAbweichendemInhalt_WirdZusaetzlichGelesenUndBenannt()
    {
        // Nicht wegwerfen, was der Sieger nicht nachweislich enthaelt.
        var gross = Merkmale(untersuchungen: 48, kanal: 209, schacht: 0, dateien: 220, fingerabdruck: "ZONE");
        var anders = Merkmale(untersuchungen: 48, kanal: 100, schacht: 300, dateien: 0, fingerabdruck: "ZONE");

        var wahl = XtfExportAuswahl.Waehle([
            new XtfExportKandidat(@"C:\x\gross.xtf", gross),
            new XtfExportKandidat(@"C:\x\anders.xtf", anders)
        ]);

        Assert.Equal(2, wahl.Uebernommen.Count);
        Assert.Contains(wahl.Entscheide,
            e => e.Pfad.EndsWith("anders.xtf", StringComparison.Ordinal)
                 && e.Grund.Contains("abweichender Inhalt", StringComparison.Ordinal));
    }

    [Fact]
    public void ReineKatasterdatei_WirdMitBegruendungAbgelehnt()
    {
        var kataster = new XtfQuellenmerkmale(["SIA405_ABWASSER_2020_LV95"], 0, 0, 0, 42);

        var wahl = XtfExportAuswahl.Waehle([new XtfExportKandidat(@"C:\x\kataster.xtf", kataster)]);

        Assert.Empty(wahl.Uebernommen);
        var entscheid = Assert.Single(wahl.Entscheide);
        Assert.Contains("reine Katasterdaten", entscheid.Grund, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Erkennung des Ordners
    // ---------------------------------------------------------------------

    [Fact]
    public void WinCanVxOrdner_MeldetSdfUndAlteXtfStattLeeremGrund()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ap2-vx-{Guid.NewGuid():N}");
        var dbDir = Path.Combine(root, "Projects", "Zone", "db");
        var exchangeDir = Path.Combine(root, "Projects", "Zone", "Misc", "Exchange", "Zone");
        Directory.CreateDirectory(dbDir);
        Directory.CreateDirectory(exchangeDir);
        File.WriteAllText(Path.Combine(dbDir, "Zone.sdf"), "kuenstliche SQL-Compact-Datei");
        File.WriteAllText(Path.Combine(dbDir, "Zone_Meta.sdf"), "Metadatenbank");
        AlteVsaKekTestquelle.SchreibeDatei(
            Path.Combine(exchangeDir, "Zone.xtf"),
            haltungen: [new AlteVsaKekTestquelle.Haltung("chU1", "100-200", "100", "200", AnzahlKanalschaeden: 2)],
            schaechte: [new AlteVsaKekTestquelle.Schacht("chS1", "3133", AnzahlSchachtschaeden: 8)]);

        try
        {
            var erkennung = new KanalExportDetectionService().Detect(root);

            // Der alte Weg bleibt bis Arbeitspaket 3 gesperrt — sonst landeten die
            // Schachtbegehungen als Haltungen im Projekt.
            Assert.Equal(KanalExportFormat.Unknown, erkennung.Format);

            // Aber der Grund sagt jetzt, WAS im Ordner liegt.
            Assert.Contains("Zone.sdf", erkennung.Reason!, StringComparison.Ordinal);
            Assert.Contains("2 Untersuchungen", erkennung.Reason!, StringComparison.Ordinal);
            Assert.Contains("VSA_KEK", erkennung.Reason!, StringComparison.Ordinal);

            Assert.EndsWith("Zone.sdf", erkennung.SdfPath!, StringComparison.OrdinalIgnoreCase);

            // Kandidatenliste: die Metadatenbank taucht nicht auf, die Fachquellen schon.
            Assert.Contains(erkennung.Kandidaten, k => k.Pfad.EndsWith("Zone.sdf", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(erkennung.Kandidaten, k => k.Pfad.EndsWith("Zone_Meta.sdf", StringComparison.OrdinalIgnoreCase));
            var xtf = Assert.Single(erkennung.Kandidaten.Where(k => k.Art == KanalQuellenart.Xtf));
            Assert.Contains("Inspektion", xtf.Typ, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(xtf.Grund));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DefekteXtf_WirdImGrundNamentlichGenannt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ap2-defekt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "kaputt.xtf"), "<TRANSFER><abgeschnitten");

        try
        {
            var erkennung = new KanalExportDetectionService().Detect(root);

            Assert.Equal(KanalExportFormat.Unknown, erkennung.Format);
            Assert.Contains("nicht lesbar", erkennung.Reason!, StringComparison.Ordinal);
            Assert.Contains("kaputt.xtf", erkennung.Reason!, StringComparison.Ordinal);
            var kandidat = Assert.Single(erkennung.Kandidaten);
            Assert.False(kandidat.Uebernommen);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void OrdnerOhneFachdaten_BehaeltDenBisherigenGrund()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ap2-leer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "liesmich.txt"), "nichts fachliches");

        try
        {
            var erkennung = new KanalExportDetectionService().Detect(root);

            Assert.Equal(KanalExportFormat.Unknown, erkennung.Format);
            Assert.Equal("Kein WinCan (.db3/.mdb in DB/) und kein IKAS/IBAK-Signal gefunden", erkennung.Reason);
            Assert.Empty(erkennung.Kandidaten);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    // ---------------------------------------------------------------------
    // Hilfsmittel
    // ---------------------------------------------------------------------

    private static XtfQuellenmerkmale Merkmale(
        int untersuchungen, int kanal, int schacht, int dateien, string fingerabdruck)
        => new(["VSA_KEK"], untersuchungen, kanal, schacht, 0)
        {
            Dateiverweise = dateien,
            Untersuchungsfingerabdruck = fingerabdruck
        };

    private static void WithDatei(string inhalt, Action<string> pruefung)
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"ap2-xtf-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(pfad, inhalt);
        try { pruefung(pfad); }
        finally { try { File.Delete(pfad); } catch { } }
    }
}
