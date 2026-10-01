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
/// Regel seither (Entscheid Pascal, «Variante C»): Haupt-Untersuchung ist die
/// vollstaendigste — nicht abgebrochen (kein BDC) vor abgebrochen, dann die laengere
/// Strecke, dann das glaubwuerdige Datum wie bei WinCan, dann die erste in der Datei. Sie
/// liefert Felder, Befunde und Protokoll. Jede weitere Untersuchung wird wie beim
/// WinCan-Import als zusaetzliche Protokollfassung (ProtocolRevision mit
/// ImportFingerprint und ImportVideoPaths) abgelegt.
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
    public void Die_vollstaendige_Befahrung_ist_Haupt_Untersuchung_auch_wenn_die_Gegenbefahrung_neuer_ist()
    {
        // Gleiche Bezeichnung wie in einer Hin- und Gegenbefahrung: Frueher trug der
        // Datensatz dann die Befunde beider Untersuchungen und die Felder der letzten.
        var (projekt, _) = Importiere(new Datei { ZweiteBezeichnung = "200-201" });

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("12.03.2025", haltung.GetFieldValue(FieldKeys.InspectionYear));
        Assert.Equal("38.40", haltung.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.Equal("In Fliessrichtung", haltung.GetFieldValue("Inspektionsrichtung"));
        Assert.Equal("refERSTE", haltung.XtfHerkunft?.UntersuchungTid);
        Assert.EndsWith("erste.mpg", haltung.GetFieldValue(FieldKeys.Link), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kanal-TV Muster AG", haltung.GetFieldValue("Bemerkungen"), StringComparison.Ordinal);

        // Befunde ueber ihre Untersuchung, nicht ueber den gemeinsamen Namen.
        Assert.NotNull(haltung.VsaFindings);
        Assert.Equal(3, haltung.VsaFindings!.Count);
        Assert.All(haltung.VsaFindings, f => Assert.Equal("refERSTE", f.UntersuchungTid));

        var primaere = haltung.GetFieldValue("Primaere_Schaeden");
        Assert.Contains("BABBA", primaere, StringComparison.Ordinal);
        Assert.DoesNotContain("BDC", primaere, StringComparison.Ordinal);
        Assert.Equal(new[] { "BCD", "BABBA", "BCE" }, haltung.Protocol!.Current.Entries.Select(e => e.Code));
    }

    [Fact]
    public void Nicht_abgebrochen_geht_vor_der_laengeren_Strecke()
    {
        // Die abgebrochene ist laenger und neuer, verliert aber trotzdem.
        var (projekt, _) = Importiere(new Datei
        {
            ErsteZeitpunkt = "20250320", ErsteAbgebrochen = true,
            ZweiteZeitpunkt = "20250101", ZweiteAbgebrochen = false
        });

        Assert.Equal("refZWEITE", Assert.Single(projekt.Data).XtfHerkunft?.UntersuchungTid);
    }

    [Fact]
    public void Die_laengere_Strecke_geht_vor_dem_neueren_Datum()
    {
        var (projekt, _) = Importiere(new Datei
        {
            ErsteZeitpunkt = "20250320", ErsteLaenge = "12.00",
            ZweiteZeitpunkt = "20250101", ZweiteLaenge = "38.40", ZweiteAbgebrochen = false
        });

        Assert.Equal("refZWEITE", Assert.Single(projekt.Data).XtfHerkunft?.UntersuchungTid);
    }

    [Theory]
    [InlineData("20250312", "20250315", "refZWEITE")]
    // Roh verglichen waere 2007-12-31 neuer als 2005-06-01; es ist aber ein Platzhalter.
    [InlineData("20071231", "20050601", "refZWEITE")]
    [InlineData("19850101", "19870101", "refERSTE")]
    [InlineData("20250312", "20250312", "refERSTE")]
    [InlineData("", "", "refERSTE")]
    public void Bei_gleicher_Vollstaendigkeit_entscheidet_das_glaubwuerdige_Datum_dann_die_Datei(
        string erste, string zweite, string erwartet)
    {
        var (projekt, _) = Importiere(new Datei
        {
            ErsteZeitpunkt = erste, ZweiteZeitpunkt = zweite,
            ZweiteLaenge = "38.40", ZweiteAbgebrochen = false
        });

        Assert.Equal(erwartet, Assert.Single(projekt.Data).XtfHerkunft?.UntersuchungTid);
    }

    [Fact]
    public void Die_weitere_Untersuchung_wird_als_Protokollfassung_mit_eigenen_Befunden_abgelegt()
    {
        var (projekt, _) = Importiere(new Datei());

        var protokoll = Assert.Single(projekt.Data).Protocol;
        Assert.NotNull(protokoll);
        var fassung = Assert.Single(protokoll!.History);
        Assert.False(string.IsNullOrWhiteSpace(fassung.ImportFingerprint));
        Assert.Contains("refZWEITE", fassung.Comment, StringComparison.Ordinal);
        Assert.Equal(new[] { "BCD", "BDC" }, fassung.Entries.Select(e => e.Code));
        // Eigene Meter der Gegenbefahrung, nicht umgerechnet.
        Assert.Equal(12.0, fassung.Entries[1].MeterStart);
        var video = Assert.Single(fassung.ImportVideoPaths!);
        Assert.EndsWith("zweite.mp4", video, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fassung.Entries[0].FotoPaths);
    }

    [Fact]
    public void Ein_Wiederholungsimport_legt_keine_zweite_Fassung_an_und_vermischt_nichts()
    {
        var projekt = new Project { Name = "Test" };
        Importiere(new Datei(), projekt);
        var (_, stats) = Importiere(new Datei(), projekt);

        var haltung = Assert.Single(projekt.Data);
        var fassung = Assert.Single(haltung.Protocol!.History);
        // Das Foto gehoert zum BCD der Haupt-Untersuchung; der Abgleich beim zweiten Import
        // darf es nicht in die Gegenbefahrung tragen.
        Assert.Empty(fassung.Entries[0].FotoPaths);
        Assert.Single(haltung.Protocol.Current.Entries[0].FotoPaths);
        Assert.Contains(stats.Messages, m => m.Message.Contains("bereits als Protokollfassung vorhanden", StringComparison.Ordinal));
    }

    [Fact]
    public void Der_Importbericht_nennt_Haupt_Untersuchung_und_zusaetzliche_Protokollfassung()
    {
        var (_, stats) = Importiere(new Datei());

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("Haupt-Untersuchung", StringComparison.Ordinal));
        Assert.Contains("200-201", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("Haupt-Untersuchung: 12.03.2025 (TID refERSTE) mit 3 Befunden", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("zusätzliche Protokollfassung: 15.03.2025 (TID refZWEITE) mit 2 Befunden", meldung.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("übersprungen", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("in_Fliessrichtung", "gegen_Fliessrichtung")]
    [InlineData("gegen_Fliessrichtung", "in_Fliessrichtung")]
    public void Das_Video_einer_Gegenbefahrung_steht_wie_bei_WinCan_in_Link_G(string ersteRichtung, string zweiteRichtung)
    {
        var (projekt, stats) = Importiere(new Datei { ErsteRichtung = ersteRichtung, ZweiteRichtung = zweiteRichtung });

        var haltung = Assert.Single(projekt.Data);
        Assert.EndsWith("erste.mpg", haltung.GetFieldValue(FieldKeys.Link), StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("zweite.mp4", haltung.GetFieldValue("Link_G"), StringComparison.OrdinalIgnoreCase);
        // Das Video bleibt zusaetzlich an seiner Protokollfassung.
        Assert.EndsWith("zweite.mp4", Assert.Single(Assert.Single(haltung.Protocol!.History).ImportVideoPaths!), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(stats.Messages, m => m.Message.Contains("Haupt-Untersuchung", StringComparison.Ordinal)
                                             && m.Message.Contains("Link_G", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("in_Fliessrichtung", "in_Fliessrichtung")]   // gleiche Richtung: Wiederholung, keine Gegenbefahrung
    [InlineData("", "gegen_Fliessrichtung")]                 // Richtung der Haupt-Untersuchung unbekannt
    [InlineData("in_Fliessrichtung", "")]                    // Richtung der weiteren unbekannt
    public void Ohne_belegte_Gegenrichtung_bleibt_Link_G_leer(string ersteRichtung, string zweiteRichtung)
    {
        var (projekt, stats) = Importiere(new Datei { ErsteRichtung = ersteRichtung, ZweiteRichtung = zweiteRichtung });

        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("", haltung.GetFieldValue("Link_G"));
        Assert.EndsWith("zweite.mp4", Assert.Single(Assert.Single(haltung.Protocol!.History).ImportVideoPaths!), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("Link_G", StringComparison.Ordinal));
    }

    /// <summary>
    /// Entscheid Pascal 01.10.2026: Eine Untersuchung aus der Gegenrichtung ist nur dann eine
    /// Gegenbefahrung, wenn beide nah beieinander liegen (hoechstens 30 Tage). Eine Befahrung aus
    /// einem anderen Jahr bleibt nur Protokollfassung. Fehlt ein glaubwuerdiges Datum: nicht raten.
    /// </summary>
    [Theory]
    [InlineData("20250312", "20250501", false)] // 50 Tage: andere Kampagne
    [InlineData("20250301", "20250331", true)]  // genau 30 Tage: noch Gegenbefahrung
    [InlineData("20250331", "20250301", true)]  // Reihenfolge egal
    [InlineData("20250312", "", false)]         // Datum der weiteren fehlt
    [InlineData("20071231", "20071231", false)] // WinCan-Platzhalterdatum
    public void Link_G_nur_bei_hoechstens_30_Tagen_Abstand(string ersteDatum, string zweiteDatum, bool gegenbefahrung)
    {
        var (projekt, _) = Importiere(new Datei
        {
            ErsteRichtung = "in_Fliessrichtung", ZweiteRichtung = "gegen_Fliessrichtung",
            ErsteZeitpunkt = ersteDatum, ZweiteZeitpunkt = zweiteDatum
        });

        var linkG = Assert.Single(projekt.Data).GetFieldValue("Link_G");
        if (gegenbefahrung)
            Assert.EndsWith("zweite.mp4", linkG, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Equal("", linkG);
    }

    /// <summary>
    /// Codex-Review PR #54: Belegt ein neuer Import die Gegenbefahrung nicht mehr (z. B. neue
    /// Haupt-Untersuchung einer spaeteren Kampagne), darf ein frueher automatisch aus XTF gesetztes
    /// Link_G nicht stehen bleiben. Der Merge ueberspringt leere Werte, daher ausdruecklich leeren.
    /// </summary>
    [Fact]
    public void Ein_veraltetes_automatisches_XTF_Link_G_wird_beim_Reimport_geleert()
    {
        var (projekt, _) = Importiere(new Datei { ErsteRichtung = "in_Fliessrichtung", ZweiteRichtung = "gegen_Fliessrichtung" });
        Assert.EndsWith("zweite.mp4", Assert.Single(projekt.Data).GetFieldValue("Link_G"), StringComparison.OrdinalIgnoreCase);

        Importiere(new Datei
        {
            ErsteRichtung = "in_Fliessrichtung", ZweiteRichtung = "gegen_Fliessrichtung", ZweiteZeitpunkt = "20250501"
        }, projekt);

        Assert.Equal("", Assert.Single(projekt.Data).GetFieldValue("Link_G"));
    }

    [Fact]
    public void Ein_Link_G_aus_einer_anderen_Quelle_bleibt_beim_XTF_Reimport_stehen()
    {
        var projekt = new Project { Name = "Test" };
        var vorhanden = new HaltungRecord();
        vorhanden.SetFieldValue(FieldKeys.HoldingName, "200-201", FieldSource.Manual, userEdited: false);
        vorhanden.SetFieldValue("Link_G", @"Videos\wincan_g.mp4", FieldSource.Legacy, userEdited: false);
        projekt.Data.Add(vorhanden);

        Importiere(new Datei { ZweiteZeitpunkt = "20250501" }, projekt);

        Assert.Equal(@"Videos\wincan_g.mp4", Assert.Single(projekt.Data).GetFieldValue("Link_G"));
    }

    [Fact]
    public void Ein_von_Hand_gesetztes_Link_G_bleibt_stehen()
    {
        var projekt = new Project { Name = "Test" };
        var vorhanden = new HaltungRecord();
        vorhanden.SetFieldValue(FieldKeys.HoldingName, "200-201", FieldSource.Manual, userEdited: false);
        vorhanden.SetFieldValue("Link_G", @"Videos\handgewaehlt.mp4", FieldSource.Manual, userEdited: true);
        projekt.Data.Add(vorhanden);

        Importiere(new Datei(), projekt);

        Assert.Equal(@"Videos\handgewaehlt.mp4", Assert.Single(projekt.Data).GetFieldValue("Link_G"));
    }

    private (Project Projekt, ImportStats Stats) Importiere(Datei datei, Project? projekt = null)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Film"));
        Directory.CreateDirectory(Path.Combine(_dir, "Fotos"));
        File.WriteAllBytes(Path.Combine(_dir, "Film", "erste.mpg"), [0x00]);
        File.WriteAllBytes(Path.Combine(_dir, "Film", "zweite.mp4"), [0x00]);
        File.WriteAllBytes(Path.Combine(_dir, "Fotos", "anfang.jpg"), [0x00]);
        var pfad = Path.Combine(_dir, "mehrere.xtf");
        File.WriteAllText(pfad, datei.Xtf());

        projekt ??= new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages.Select(m => m.Message)));
        return (projekt, stats);
    }

    /// <summary>
    /// Zwei Untersuchungen derselben Haltung. Voreinstellung: die erste ist die vollstaendige
    /// Hinfahrt (38,40 m, 3 Befunde), die zweite eine neuere, abgebrochene Gegenbefahrung
    /// (12,00 m, BCD + BDC) mit anders geschriebener, gleich normalisierter Bezeichnung.
    /// </summary>
    private sealed class Datei
    {
        public string ErsteZeitpunkt { get; init; } = "20250312";
        public string ErsteLaenge { get; init; } = "38.40";
        public bool ErsteAbgebrochen { get; init; }
        public string ZweiteBezeichnung { get; init; } = "200 - 201";
        public string ZweiteZeitpunkt { get; init; } = "20250315";
        public string ZweiteLaenge { get; init; } = "12.00";
        public bool ZweiteAbgebrochen { get; init; } = true;
        public string ErsteRichtung { get; init; } = "in_Fliessrichtung";
        public string ZweiteRichtung { get; init; } = "gegen_Fliessrichtung";

        private static string Zeitpunkt(string wert) => wert.Length == 0 ? "" : $"<Zeitpunkt>{wert}</Zeitpunkt>";

        private static string Schaden(string tid, string untersuchung, string code, string distanz) => $"""
                  <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="{tid}">
                    <UntersuchungRef REF="{untersuchung}" />
                    <KanalSchadencode>{code}</KanalSchadencode>
                    <Distanz>{distanz}</Distanz>
                  </VSA_KEK_2020_LV95.KEK.Kanalschaden>
            """;

        public string Xtf() => $"""
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
                    {Zeitpunkt(ErsteZeitpunkt)}
                    <Inspizierte_Laenge>{ErsteLaenge}</Inspizierte_Laenge>
                    <Erfassungsart>Kanalfernsehen</Erfassungsart>
                    <vonPunktBezeichnung>200</vonPunktBezeichnung>
                    <bisPunktBezeichnung>201</bisPunktBezeichnung>
                    <Fliessrichtung>{ErsteRichtung}</Fliessrichtung>
                  </VSA_KEK_2020_LV95.KEK.Untersuchung>
                  <VSA_KEK_2020_LV95.KEK.Untersuchung TID="refZWEITE">
                    <Bezeichnung>{ZweiteBezeichnung}</Bezeichnung>
                    {Zeitpunkt(ZweiteZeitpunkt)}
                    <Inspizierte_Laenge>{ZweiteLaenge}</Inspizierte_Laenge>
                    <Erfassungsart>Kanalfernsehen</Erfassungsart>
                    <vonPunktBezeichnung>201</vonPunktBezeichnung>
                    <bisPunktBezeichnung>200</bisPunktBezeichnung>
                    <Fliessrichtung>{ZweiteRichtung}</Fliessrichtung>
                  </VSA_KEK_2020_LV95.KEK.Untersuchung>
            {Schaden("refS1", "refERSTE", "BCD", "0.00")}
            {Schaden("refS2", "refERSTE", "BABBA", "12.30")}
            {Schaden("refS3", "refERSTE", ErsteAbgebrochen ? "BDC" : "BCE", "38.40")}
            {Schaden("refS4", "refZWEITE", "BCD", "0.00")}
            {Schaden("refS5", "refZWEITE", ZweiteAbgebrochen ? "BDC" : "BCE", "12.00")}
                  <VSA_KEK_2020_LV95.KEK.Datei TID="refD0">
                    <Art>Foto</Art>
                    <Klasse>Kanalschaden</Klasse>
                    <Objekt>refS1</Objekt>
                    <Bezeichnung>anfang.jpg</Bezeichnung>
                    <Relativpfad>Fotos</Relativpfad>
                  </VSA_KEK_2020_LV95.KEK.Datei>
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
}
