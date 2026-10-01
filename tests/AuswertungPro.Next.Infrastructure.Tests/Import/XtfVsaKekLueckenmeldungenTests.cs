using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Was der VSA-KEK-Import keiner Haltung und keinem Schacht zuordnen kann, steht im
/// Importbericht (Auftrag Pascal 01.10.2026, Befunde 5 und 6 aus AP08). Bis dahin zaehlte
/// eine Untersuchung ohne Bezeichnung nur in «N Untersuchungen gelesen», und verwaiste
/// Schaeden und Dateien fielen still weg. Uebernommen wird weiterhin nichts davon.
/// </summary>
public sealed class XtfVsaKekLueckenmeldungenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_luecken_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Eine_Untersuchung_ohne_Bezeichnung_steht_mit_TID_im_Importbericht()
    {
        var (projekt, stats) = ImportiereReferenz();

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("TID refUNTERS8", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF", meldung.Context);
        Assert.Contains("Untersuchung ohne Bezeichnung", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("nicht übernommen", meldung.Message, StringComparison.Ordinal);
        // Nur gemeldet: kein Datensatz und kein zusaetzlicher «ungeklaert»-Fall.
        Assert.DoesNotContain(projekt.Data, r => r.XtfHerkunft?.UntersuchungTid == "refUNTERS8");
        Assert.Equal(3, stats.Uncertain);
    }

    [Theory]
    [InlineData("Kanalschaden (TID refSCHADENVERWAIST", "Untersuchungsverweis refUNTERSFEHLT zeigt ins Leere")]
    [InlineData("Kanalschaden (TID refSCHADENOHNEREF", "Untersuchungsverweis fehlt")]
    [InlineData("Normschachtschaden (TID refSCHSCHADENVERWAIST", "Untersuchungsverweis refUNTERSFEHLT zeigt ins Leere")]
    [InlineData("Datei \"foto_verwaist.jpg\" (TID refDATEI4", "Kanalschadenverweis refSCHADENFEHLT zeigt ins Leere")]
    public void Ein_verwaister_Schaden_oder_eine_verwaiste_Datei_steht_mit_TID_im_Importbericht(string objekt, string grund)
    {
        var (_, stats) = ImportiereReferenz();

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains(objekt, StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF", meldung.Context);
        Assert.Contains("nicht übernommen", meldung.Message, StringComparison.Ordinal);
        Assert.Contains(grund, meldung.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Zugeordnete_Dateien_und_ein_zweites_Foto_desselben_Schadens_sind_nicht_verwaist()
    {
        var (_, stats) = ImportiereReferenz();

        foreach (var kennung in new[] { "foto_schaden2_zweites.jpg", "refDATEI3", "refDATEI5", "refDATEI6", "refDATEI7", "refSCHSCHADEN3" })
            Assert.DoesNotContain(stats.Messages, m => m.Message.Contains(kennung, StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Video_zu_einer_fehlenden_Untersuchung_ist_verwaist()
    {
        var (projekt, stats) = Importiere(Schreibe("video.xtf", Xtf(
            Schaden("refS1", "refU1", "BCD"),
            """
                  <VSA_KEK_2020_LV95.KEK.Datei TID="refDV">
                    <Art>Video</Art>
                    <Klasse>Untersuchung</Klasse>
                    <Objekt>refUFEHLT</Objekt>
                    <Bezeichnung>fremd.mp4</Bezeichnung>
                    <Relativpfad>Film</Relativpfad>
                  </VSA_KEK_2020_LV95.KEK.Datei>
            """)));

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("TID refDV", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Contains("Datei \"fremd.mp4\"", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("Untersuchungsverweis refUFEHLT zeigt ins Leere", meldung.Message, StringComparison.Ordinal);
        Assert.Equal("", Assert.Single(projekt.Data).GetFieldValue(FieldKeys.Link));
    }

    [Fact]
    public void Viele_verwaiste_Schaeden_stehen_gebuendelt_in_einer_Meldung()
    {
        var verwaiste = Enumerable.Range(1, 12).Select(i => Schaden($"refV{i:00}", "refFEHLT", "BAB")).ToArray();
        var (_, stats) = Importiere(Schreibe("viele.xtf", Xtf([Schaden("refS1", "refU1", "BCD"), .. verwaiste])));

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("verwaiste Kanalschäden", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.StartsWith("12 verwaiste Kanalschäden nicht übernommen", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("refV01, refV02", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("refV10 (+2 weitere)", meldung.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("refV11", meldung.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(stats.Messages, m => m.Message.StartsWith("Kanalschaden (TID", StringComparison.Ordinal));
    }

    private string Schreibe(string name, string inhalt)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, name);
        File.WriteAllText(pfad, inhalt);
        return pfad;
    }

    private static string Schaden(string tid, string untersuchung, string code) => $"""
              <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="{tid}">
                <UntersuchungRef REF="{untersuchung}" />
                <KanalSchadencode>{code}</KanalSchadencode>
                <Distanz>0.00</Distanz>
              </VSA_KEK_2020_LV95.KEK.Kanalschaden>
        """;

    /// <summary>Eine Haltungsuntersuchung refU1 «100-101» und die uebergebenen Objekte.</summary>
    private static string Xtf(params string[] objekte) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION VERSION="2.3" SENDER="Test">
            <MODELS>
              <MODEL NAME="VSA_KEK_2020_LV95" />
            </MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <VSA_KEK_2020_LV95.KEK BID="refB1">
              <VSA_KEK_2020_LV95.KEK.Untersuchung TID="refU1">
                <Bezeichnung>100-101</Bezeichnung>
                <Zeitpunkt>20250312</Zeitpunkt>
                <Erfassungsart>Kanalfernsehen</Erfassungsart>
                <vonPunktBezeichnung>100</vonPunktBezeichnung>
                <bisPunktBezeichnung>101</bisPunktBezeichnung>
              </VSA_KEK_2020_LV95.KEK.Untersuchung>
        {string.Join("\n", objekte)}
            </VSA_KEK_2020_LV95.KEK>
          </DATASECTION>
        </TRANSFER>
        """;

    private (Project Projekt, ImportStats Stats) ImportiereReferenz()
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, "vsakek-referenz.xtf");
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "vsakek-referenz.xtf"), pfad);
        return Importiere(pfad);
    }

    private static (Project Projekt, ImportStats Stats) Importiere(string pfad)
    {
        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages.Select(m => m.Message)));
        return (projekt, stats);
    }
}
