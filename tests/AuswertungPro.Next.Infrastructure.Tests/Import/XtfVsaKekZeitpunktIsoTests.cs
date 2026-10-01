using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der VSA-KEK-<c>Zeitpunkt</c> kommt als <c>yyyymmdd</c> oder als INTERLIS-Datum im
/// ISO-Format (<c>2025-03-12</c>, auch mit Uhrzeit, Zone oder Versatz). Bis 01.10.2026 (zweite
/// Runde) blieb das ISO-Datum roh in <c>Datum_Jahr</c>, und mit Zone oder Versatz kannte auch
/// die Wahl der Haupt-Untersuchung, die 30-Tage-Regel fuer Link_G und der Importbericht das
/// Datum nicht. Jetzt gilt dieselbe Lesart wie bei <c>Letzte_Aenderung</c>
/// (<c>XtfValueNormalizer.NormalizeDate</c>). Unlesbares bleibt wie bisher roh.
///
/// Der Importbeleg (<c>ImportFingerprint</c>) rechnet weiter mit dem Rohwert: nur Lesen und
/// Anzeige aendern sich, gespeicherte Belege bleiben gueltig.
/// </summary>
public sealed class XtfVsaKekZeitpunktIsoTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_zeitpunkt_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Theory]
    [InlineData("20250312", "12.03.2025")]                // wie bisher
    [InlineData("2025-03-12", "12.03.2025")]
    [InlineData("2025-03-12T08:15:00", "12.03.2025")]
    [InlineData("2025-03-12T08:15:00Z", "12.03.2025")]
    [InlineData("2025-03-12 08:15", "12.03.2025")]
    [InlineData("2025-03-12T08:15:00.5+01:00", "12.03.2025")]
    [InlineData("2007-12-31", "31.12.2007")]              // Platzhalter wie bei 20071231
    [InlineData("März 2025", "März 2025")]                // unlesbar: roh wie bisher
    [InlineData("2025-13-06", "2025-13-06")]              // ungueltig: roh wie bisher
    public void Datum_Jahr_wird_unabhaengig_vom_Format_zu_dd_MM_yyyy(string zeitpunkt, string erwartet)
    {
        var (projekt, _) = Importiere(Haltungsdatei(zeitpunkt, "2025-03-15T10:00:00Z"));

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("refERSTE", haltung.XtfHerkunft?.UntersuchungTid);
        Assert.Equal(erwartet, haltung.GetFieldValue(FieldKeys.InspectionYear));
    }

    [Theory]
    [InlineData("2025-03-12T08:15:00Z", 2025, 3, 12)]
    [InlineData("2025-03-12 08:15", 2025, 3, 12)]
    [InlineData("2025-03-12T08:15:00+01:00", 2025, 3, 12)]
    [InlineData("20250312", 2025, 3, 12)]
    public void LiesZeitpunkt_kennt_das_ISO_Datum_mit_Zone_und_Versatz(string roh, int jahr, int monat, int tag)
        => Assert.Equal(new DateTime(jahr, monat, tag), VsaKekUntersuchungsWahl.LiesZeitpunkt(roh)?.Date);

    [Theory]
    [InlineData("März 2025")]
    [InlineData("2025-13-06")]
    [InlineData("")]
    public void LiesZeitpunkt_liest_Unlesbares_weiter_nicht(string roh)
        => Assert.Null(VsaKekUntersuchungsWahl.LiesZeitpunkt(roh));

    [Fact]
    public void Bericht_Wahl_und_Link_G_kennen_das_ISO_Datum_mit_Zone()
    {
        // Beide gleich lang: das neuere glaubwuerdige Datum gewinnt, Abstand 3 Tage = Gegenbefahrung.
        var (projekt, stats) = Importiere(Haltungsdatei("2025-03-12T08:00:00Z", "2025-03-15T09:00:00+01:00", zweiteLaenge: "38.40"));

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("refZWEITE", haltung.XtfHerkunft?.UntersuchungTid);
        Assert.Equal("15.03.2025", haltung.GetFieldValue(FieldKeys.InspectionYear));
        Assert.EndsWith("erste.mp4", haltung.GetFieldValue("Link_G"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(stats.Messages, m => m.Message.Contains("Haupt-Untersuchung: 15.03.2025 (TID refZWEITE)", StringComparison.Ordinal)
                                             && m.Message.Contains("zusätzliche Protokollfassung: 12.03.2025 (TID refERSTE)", StringComparison.Ordinal));
    }

    [Fact]
    public void Link_G_bleibt_bei_ISO_Daten_mehr_als_30_Tage_auseinander_leer()
    {
        var (projekt, _) = Importiere(Haltungsdatei("2025-03-12T08:00:00Z", "2025-05-01T08:00:00Z"));

        Assert.Equal("", Assert.Single(projekt.Data).GetFieldValue("Link_G"));
    }

    [Fact]
    public void Schachtbegehung_zeigt_das_ISO_Datum_lesbar_und_ihr_Importbeleg_rechnet_mit_dem_Rohwert()
    {
        const string roh = "2025-03-13T09:30:00Z";
        var inhalt = File.ReadAllText(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "vsakek-referenz.xtf"))
            .Replace("<Zeitpunkt>20250313</Zeitpunkt>", $"<Zeitpunkt>{roh}</Zeitpunkt>", StringComparison.Ordinal);
        Assert.Contains(roh, inhalt, StringComparison.Ordinal);

        var (projekt, stats) = Importiere(inhalt);

        var schacht = Assert.Single(projekt.SchaechteData, s => s.GetFieldValue("Schachtnummer") == "300");
        Assert.Equal("13.03.2025", schacht.GetFieldValue(FieldKeys.InspectionYear));
        Assert.Equal("Import aus VSA-KEK-XTF (Schachtbegehung vom 13.03.2025)", schacht.Protocol!.Original!.Comment);

        // Der Beleg ist SHA-256 ueber die gelesene Untersuchung samt Schachtschaeden, mit dem
        // unveraenderten Rohwert (dieselben zwei Schritte wie der Import: lesen, zuordnen).
        var bezuege = VsaKekBeziehungen.Loese(VsaKekObjektLeser.Lies(XDocument.Parse(inhalt)),
            Path.Combine(_dir, "zeitpunkt.xtf"), VsaMediaPathResolver.Current);
        var untersuchung = Assert.Single(bezuege.Schachtbegehungen, u => u.Tid == "refUNTERS3");
        Assert.Equal(roh, untersuchung.Zeitpunkt);
        var beleg = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(untersuchung)));
        Assert.Equal(beleg, schacht.Protocol.Original.ImportFingerprint);
        Assert.Equal(0, stats.Errors);
    }

    private (Project Projekt, ImportStats Stats) Importiere(string inhalt)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Film"));
        File.WriteAllBytes(Path.Combine(_dir, "Film", "erste.mp4"), [0x00]);
        File.WriteAllBytes(Path.Combine(_dir, "Film", "zweite.mp4"), [0x00]);
        var pfad = Path.Combine(_dir, "zeitpunkt.xtf");
        File.WriteAllText(pfad, inhalt);

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        return (projekt, stats);
    }

    /// <summary>
    /// Zwei Untersuchungen derselben Haltung aus Gegenrichtungen, je mit Video. Voreinstellung:
    /// die erste ist laenger und damit Haupt-Untersuchung.
    /// </summary>
    private static string Haltungsdatei(string ersteZeitpunkt, string zweiteZeitpunkt, string zweiteLaenge = "12.00") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION VERSION="2.3" SENDER="Test">
            <MODELS>
              <MODEL NAME="VSA_KEK_2020_LV95" />
            </MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <VSA_KEK_2020_LV95.KEK BID="refB1">
              <VSA_KEK_2020_LV95.KEK.Untersuchung TID="refERSTE">
                <Bezeichnung>200-201</Bezeichnung>
                <Zeitpunkt>{ersteZeitpunkt}</Zeitpunkt>
                <Inspizierte_Laenge>38.40</Inspizierte_Laenge>
                <vonPunktBezeichnung>200</vonPunktBezeichnung>
                <bisPunktBezeichnung>201</bisPunktBezeichnung>
                <Fliessrichtung>in_Fliessrichtung</Fliessrichtung>
              </VSA_KEK_2020_LV95.KEK.Untersuchung>
              <VSA_KEK_2020_LV95.KEK.Untersuchung TID="refZWEITE">
                <Bezeichnung>200-201</Bezeichnung>
                <Zeitpunkt>{zweiteZeitpunkt}</Zeitpunkt>
                <Inspizierte_Laenge>{zweiteLaenge}</Inspizierte_Laenge>
                <vonPunktBezeichnung>201</vonPunktBezeichnung>
                <bisPunktBezeichnung>200</bisPunktBezeichnung>
                <Fliessrichtung>gegen_Fliessrichtung</Fliessrichtung>
              </VSA_KEK_2020_LV95.KEK.Untersuchung>
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="refS1">
                <UntersuchungRef REF="refERSTE" />
                <KanalSchadencode>BCD</KanalSchadencode>
                <Distanz>0.00</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="refS2">
                <UntersuchungRef REF="refZWEITE" />
                <KanalSchadencode>BCD</KanalSchadencode>
                <Distanz>0.00</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Datei TID="refD1">
                <Art>Video</Art>
                <Klasse>Untersuchung</Klasse>
                <Objekt>refERSTE</Objekt>
                <Bezeichnung>erste.mp4</Bezeichnung>
                <Relativpfad>Film</Relativpfad>
              </VSA_KEK_2020_LV95.KEK.Datei>
              <VSA_KEK_2020_LV95.KEK.Datei TID="refD2">
                <Art>Video</Art>
                <Klasse>Untersuchung</Klasse>
                <Objekt>refZWEITE</Objekt>
                <Bezeichnung>zweite.mp4</Bezeichnung>
                <Relativpfad>Film</Relativpfad>
              </VSA_KEK_2020_LV95.KEK.Datei>
            </VSA_KEK_2020_LV95.KEK>
          </DATASECTION>
        </TRANSFER>
        """;
}
