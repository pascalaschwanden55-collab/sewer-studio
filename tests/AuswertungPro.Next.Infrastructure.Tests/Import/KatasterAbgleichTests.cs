using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Kataster;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Kataster;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Abgleich der Haltungsnummern gegen den amtlichen Kataster.
///
/// Grundlage bleibt das Protokoll (Schacht oben - Schacht unten). Der Kataster korrigiert
/// nur dort, wo er dasselbe Schachtpaar eindeutig unter einem anderen Namen fuehrt.
/// Echter Fall Andermatt: 13 von 15 Haltungen stimmen exakt, zwei heissen amtlich anders
/// (z. B. "7.4790-4789" statt "955509-4789").
/// </summary>
public sealed class KatasterAbgleichTests
{
    private static Project ProjektMit(params (string Name, string Oben, string Unten)[] haltungen)
    {
        var p = new Project();
        foreach (var (name, oben, unten) in haltungen)
        {
            var r = p.CreateNewRecord();
            r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Pdf, userEdited: false);
            r.SetFieldValue("Schacht_oben", oben, FieldSource.Pdf, userEdited: false);
            r.SetFieldValue("Schacht_unten", unten, FieldSource.Pdf, userEdited: false);
            p.AddRecord(r);
        }

        return p;
    }

    private static IKatasterHaltungsverzeichnis Kataster(params (string O, string U, string Bez)[] eintraege)
        => new KatasterHaltungsverzeichnis(
            eintraege.Select(e => new KatasterHaltung(e.O, e.U, e.Bez)));

    private static string? Name(Project p, string schachtOben)
        => p.Data.FirstOrDefault(r =>
            string.Equals(r.GetFieldValue("Schacht_oben"), schachtOben, StringComparison.OrdinalIgnoreCase))
            ?.GetFieldValue(FieldKeys.HoldingName);

    [Fact]
    public void AbweichendeAmtlicheBezeichnung_WirdUebernommenUndGemeldet()
    {
        var p = ProjektMit(("955509-4789", "955509", "4789"));

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("955509", "4789", "7.4790-4789")));

        Assert.Equal(1, ergebnis.Korrigiert);
        Assert.Equal("7.4790-4789", Name(p, "955509"));
        Assert.Contains(ergebnis.Meldungen, m =>
            m.Contains("955509-4789", StringComparison.Ordinal)
            && m.Contains("7.4790-4789", StringComparison.Ordinal));
    }

    [Fact]
    public void OhneTrefferImKataster_BleibtDieProtokollnummer()
    {
        // Ausdrueckliche Vorgabe: Was der Kataster nicht kennt, behaelt die Nummer aus
        // dem Protokoll. Es wird nichts geraten und nichts geleert.
        var p = ProjektMit(("7435-7434", "7435", "7434"));

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("1", "2", "1-2")));

        Assert.Equal(0, ergebnis.Korrigiert);
        Assert.Equal("7435-7434", Name(p, "7435"));
    }

    [Fact]
    public void GleicheBezeichnung_AendertNichts()
    {
        var p = ProjektMit(("2942-2943", "2942", "2943"));

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("2942", "2943", "2942-2943")));

        Assert.Equal(0, ergebnis.Korrigiert);
        Assert.Empty(ergebnis.Meldungen);
        Assert.Equal("2942-2943", Name(p, "2942"));
    }

    [Fact]
    public void OhneVollstaendigesSchachtpaar_WirdNichtsGeaendert()
    {
        var p = ProjektMit(("H6", "955509", ""));

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("955509", "4789", "7.4790-4789")));

        Assert.Equal(0, ergebnis.Geprueft);
        Assert.Equal("H6", Name(p, "955509"));
    }

    [Fact]
    public void VonHandBearbeiteterName_BleibtUnangetastet()
    {
        var p = new Project();
        var r = p.CreateNewRecord();
        r.SetFieldValue(FieldKeys.HoldingName, "Mein Name", FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Schacht_oben", "955509", FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Schacht_unten", "4789", FieldSource.Pdf, userEdited: false);
        p.AddRecord(r);

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("955509", "4789", "7.4790-4789")));

        Assert.Equal(0, ergebnis.Korrigiert);
        Assert.Equal(1, ergebnis.Uebersprungen);
        Assert.Equal("Mein Name", r.GetFieldValue(FieldKeys.HoldingName));
    }

    [Fact]
    public void BereitsVergebenerAmtlicherName_ErzeugtKeineDublette()
    {
        // Zwei Haltungen duerfen nie denselben Namen bekommen.
        var p = ProjektMit(
            ("7.4790-4789", "111", "222"),
            ("955509-4789", "955509", "4789"));

        var ergebnis = HaltungsnummerKatasterAbgleich.Gleiche(
            p, Kataster(("955509", "4789", "7.4790-4789")));

        Assert.Equal(0, ergebnis.Korrigiert);
        Assert.Equal(1, ergebnis.Uebersprungen);
        Assert.Equal("955509-4789", Name(p, "955509"));
        Assert.Contains(ergebnis.Meldungen, m => m.Contains("bereits vergeben", StringComparison.Ordinal));
    }

    [Fact]
    public void MehrdeutigesSchachtpaarImKataster_WirdVerworfen()
    {
        // Dasselbe Paar unter zwei Namen: lieber keine Korrektur als eine geratene.
        var verzeichnis = Kataster(
            ("955509", "4789", "7.4790-4789"),
            ("955509", "4789", "ganz-anders"));

        Assert.Equal(0, verzeichnis.Anzahl);
        Assert.Null(verzeichnis.FindeBezeichnung("955509", "4789"));
    }

    [Fact]
    public void OhneKatasterdatei_PassiertNichts()
    {
        var p = ProjektMit(("955509-4789", "955509", "4789"));

        Assert.Equal(0, HaltungsnummerKatasterAbgleich.Gleiche(p, null).Korrigiert);
        Assert.Equal("955509-4789", Name(p, "955509"));
    }

    [Fact]
    public void UnlesbareDatei_ErgibtLeeresVerzeichnis_StattAusnahme()
    {
        var datei = Path.Combine(Path.GetTempPath(), $"kein-xtf-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(datei, "das ist kein XML");
        try
        {
            Assert.Equal(0, SiaKatasterXtfReader.Lies(datei).Anzahl);
            Assert.Equal(0, SiaKatasterXtfReader.Lies(@"C:\gibt\es\nicht.xtf").Anzahl);
            Assert.Equal(0, SiaKatasterXtfReader.Lies(null).Anzahl);
        }
        finally
        {
            try { File.Delete(datei); } catch { }
        }
    }

    [Fact]
    public void LiestHaltungenAusEinerSia405Datei()
    {
        var datei = Path.Combine(Path.GetTempPath(), $"kataster-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(datei, """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3"><MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS></HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="b1">
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten TID="k1">
        <Bezeichnung>955509</Bezeichnung>
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten TID="k2">
        <Bezeichnung>4789</Bezeichnung>
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="p1">
        <Bezeichnung>7.4790-4789_von</Bezeichnung>
        <abwassernetzelementRef REF="k1" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="p2">
        <Bezeichnung>7.4790-4789_nach</Bezeichnung>
        <abwassernetzelementRef REF="k2" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="h1">
        <Bezeichnung>7.4790-4789</Bezeichnung>
        <vonHaltungspunktRef REF="p1" />
        <nachHaltungspunktRef REF="p2" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""");
        try
        {
            var verzeichnis = SiaKatasterXtfReader.Lies(datei);

            Assert.Equal(1, verzeichnis.Anzahl);
            Assert.Equal("7.4790-4789", verzeichnis.FindeBezeichnung("955509", "4789"));
            // Umgekehrtes Paar ist eine andere Haltung und darf nicht treffen.
            Assert.Null(verzeichnis.FindeBezeichnung("4789", "955509"));
        }
        finally
        {
            try { File.Delete(datei); } catch { }
        }
    }
}
