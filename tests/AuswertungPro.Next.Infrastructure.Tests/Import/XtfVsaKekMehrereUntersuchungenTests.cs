using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Mehrere VSA-KEK-Untersuchungen derselben Haltung in einer Datei (z.B. Hin- und
/// Gegenbefahrung). Bis 30.09.2026 ueberschrieb die zweite Datum, Laenge, Richtung, Video,
/// Herkunft und Bemerkung der ersten, und der Datensatz trug die Befunde beider, weil sie
/// ueber den Namen zugeordnet wurden (Befund 4 aus AP08).
///
/// Regel seither, dieselbe wie beim WinCan-Import: Je Haltung genau eine Untersuchung,
/// die mit dem neuesten glaubwuerdigen Datum. Der WinCan-Vorgabetag 2007-12-31 und alles
/// vor 1990 zaehlen als Platzhalter; bei Gleichstand gilt die Dateireihenfolge. Nur die
/// gewaehlte Untersuchung liefert Felder und Befunde; jede andere steht im Importbericht.
/// </summary>
public sealed class XtfVsaKekMehrereUntersuchungenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_mehrere_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Nur_die_neueste_Untersuchung_liefert_Felder_und_Befunde()
    {
        // Gleiche Bezeichnung wie in einer Hin- und Gegenbefahrung: Frueher trug der
        // Datensatz dann die Befunde beider Untersuchungen.
        var (projekt, _) = Importiere(ersteZeitpunkt: "20250312", zweiteZeitpunkt: "20250315",
            zweiteBezeichnung: "200-201");

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("15.03.2025", haltung.GetFieldValue(FieldKeys.InspectionYear));
        Assert.Equal("12.00", haltung.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.Equal("Gegen Fliessrichtung", haltung.GetFieldValue("Inspektionsrichtung"));
        Assert.Equal("refZWEITE", haltung.XtfHerkunft?.UntersuchungTid);
        Assert.EndsWith("zweite.mp4", haltung.GetFieldValue(FieldKeys.Link), StringComparison.OrdinalIgnoreCase);

        // Befunde ueber ihre Untersuchung, nicht ueber den gemeinsamen Namen.
        Assert.NotNull(haltung.VsaFindings);
        Assert.Equal(2, haltung.VsaFindings!.Count);
        Assert.All(haltung.VsaFindings, f => Assert.Equal("refZWEITE", f.UntersuchungTid));

        var primaere = haltung.GetFieldValue("Primaere_Schaeden");
        Assert.Contains("BDC", primaere, StringComparison.Ordinal);
        Assert.DoesNotContain("BABBA", primaere, StringComparison.Ordinal);

        // Die Bemerkung stammt ebenfalls nur von der gewaehlten Untersuchung.
        Assert.DoesNotContain("Kanal-TV Muster AG", haltung.GetFieldValue("Bemerkungen"), StringComparison.Ordinal);
    }

    [Fact]
    public void Die_uebersprungene_Untersuchung_steht_mit_Datum_im_Importbericht()
    {
        var (_, stats) = Importiere(ersteZeitpunkt: "20250312", zweiteZeitpunkt: "20250315");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("übersprungen", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Contains("200-201", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("Übernommen: 15.03.2025 (TID refZWEITE)", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("übersprungen: 12.03.2025 (TID refERSTE) mit 3 Befunden", meldung.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Platzhalterdatum_gewinnt_nie_gegen_ein_glaubwuerdiges_Datum()
    {
        // Roh verglichen waere 2007-12-31 neuer als 2005-06-01.
        var (projekt, stats) = Importiere(ersteZeitpunkt: "20071231", zweiteZeitpunkt: "20050601");

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("refZWEITE", haltung.XtfHerkunft?.UntersuchungTid);
        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("übersprungen", StringComparison.Ordinal));
        Assert.Contains("Platzhalterdatum", meldung.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("19850101", "19870101")]
    [InlineData("20250312", "20250312")]
    [InlineData("", "")]
    public void Ohne_glaubwuerdigen_Unterschied_gilt_die_Dateireihenfolge(string erste, string zweite)
    {
        var (projekt, _) = Importiere(erste, zweite);

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("refERSTE", haltung.XtfHerkunft?.UntersuchungTid);
        Assert.Equal(3, haltung.VsaFindings?.Count);
    }

    /// <summary>Die zweite Bezeichnung ist standardmaessig anders geschrieben, aber gleich normalisiert.</summary>
    private (Project Projekt, ImportStats Stats) Importiere(string ersteZeitpunkt, string zweiteZeitpunkt,
        string zweiteBezeichnung = "200 - 201")
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Film"));
        File.WriteAllBytes(Path.Combine(_dir, "Film", "erste.mpg"), [0x00]);
        File.WriteAllBytes(Path.Combine(_dir, "Film", "zweite.mp4"), [0x00]);
        var pfad = Path.Combine(_dir, "mehrere.xtf");
        File.WriteAllText(pfad, ZweiUntersuchungen(ersteZeitpunkt, zweiteZeitpunkt, zweiteBezeichnung));

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages.Select(m => m.Message)));
        return (projekt, stats);
    }

    private static string Zeitpunkt(string wert) => wert.Length == 0 ? "" : $"<Zeitpunkt>{wert}</Zeitpunkt>";

    private static string ZweiUntersuchungen(string ersteZeitpunkt, string zweiteZeitpunkt, string zweiteBezeichnung) => $"""
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
                <Ausfuehrender>Kanal-TV Muster AG</Ausfuehrender>
                {Zeitpunkt(ersteZeitpunkt)}
                <Inspizierte_Laenge>38.40</Inspizierte_Laenge>
                <Erfassungsart>Kanalfernsehen</Erfassungsart>
                <vonPunktBezeichnung>200</vonPunktBezeichnung>
                <bisPunktBezeichnung>201</bisPunktBezeichnung>
                <Fliessrichtung>in_Fliessrichtung</Fliessrichtung>
              </VSA_KEK_2020_LV95.KEK.Untersuchung>
              <VSA_KEK_2020_LV95.KEK.Untersuchung TID="refZWEITE">
                <Bezeichnung>{zweiteBezeichnung}</Bezeichnung>
                {Zeitpunkt(zweiteZeitpunkt)}
                <Inspizierte_Laenge>12.00</Inspizierte_Laenge>
                <Erfassungsart>Kanalfernsehen</Erfassungsart>
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
                <UntersuchungRef REF="refERSTE" />
                <KanalSchadencode>BABBA</KanalSchadencode>
                <Distanz>12.30</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="refS3">
                <UntersuchungRef REF="refERSTE" />
                <KanalSchadencode>BCE</KanalSchadencode>
                <Distanz>38.40</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="refS4">
                <UntersuchungRef REF="refZWEITE" />
                <KanalSchadencode>BCD</KanalSchadencode>
                <Distanz>0.00</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="refS5">
                <UntersuchungRef REF="refZWEITE" />
                <KanalSchadencode>BDC</KanalSchadencode>
                <Distanz>12.00</Distanz>
                <Anmerkung>Abbruch Gegenbefahrung</Anmerkung>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
              <VSA_KEK_2020_LV95.KEK.Datei TID="refD1">
                <Art>Video</Art>
                <Klasse>Untersuchung</Klasse>
                <Objekt>refERSTE</Objekt>
                <Bezeichnung>erste.mpg</Bezeichnung>
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
