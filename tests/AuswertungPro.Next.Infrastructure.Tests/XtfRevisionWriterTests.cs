using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Ausfuehrer schreibt ausschliesslich den Plan. Das Original bleibt unberuehrt,
/// eine vorhandene Zieldatei wird nie ueberschrieben, und alles nicht Geplante bleibt stehen.
/// </summary>
public sealed class XtfRevisionWriterTests : IDisposable
{
    private readonly string _dir;

    public XtfRevisionWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"xtf-revision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    private const string Original = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch1000A1">
        <Letzte_Aenderung>20260114</Letzte_Aenderung>
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>5.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch1000A2">
        <Letzte_Aenderung>20260114</Letzte_Aenderung>
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BBC</KanalSchadencode>
        <Distanz>9.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Bezeichnung>foto.jpg</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Eine_Aenderung_wird_geschrieben_und_datiert()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel, new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Geaendert);

        var schaden = Kanalschaden(ziel, "ch1000A1");
        Assert.Equal("BAC", Kindwert(schaden, "KanalSchadencode"));
        Assert.Equal("20260813", Kindwert(schaden, "Letzte_Aenderung"));
    }

    [Fact]
    public void Das_Original_bleibt_bytegleich()
    {
        var (quelle, ziel) = Dateien();
        var vorher = File.ReadAllBytes(quelle);
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        Assert.Equal(vorher, File.ReadAllBytes(quelle));
    }

    [Fact]
    public void Nicht_geplante_Elemente_bleiben_unveraendert_stehen()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        // Der zweite Schaden und der Dateiverweis stehen unveraendert im Ergebnis.
        Assert.Equal("BBC", Kindwert(Kanalschaden(ziel, "ch1000A2"), "KanalSchadencode"));
        Assert.Equal("20260114", Kindwert(Kanalschaden(ziel, "ch1000A2"), "Letzte_Aenderung"));
        Assert.Contains(
            XDocument.Load(ziel).Descendants().Where(e => e.Name.LocalName.EndsWith("Datei", StringComparison.Ordinal)),
            e => (string?)e.Attribute("TID") == "D1");
    }

    [Fact]
    public void Ein_entfernter_Schaden_verschwindet_aus_der_Revision()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Entfernt, "ch1000A2", "BBC"));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Entfernt);
        Assert.Null(KanalschadenOderNull(ziel, "ch1000A2"));
        Assert.NotNull(KanalschadenOderNull(ziel, "ch1000A1"));
    }

    [Fact]
    public void Ein_neuer_Schaden_bekommt_eine_freie_Kennung_und_die_richtige_Untersuchung()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Neu, null, "BAF",
            new XtfRevisionFeld("KanalSchadencode", null, "BAF"),
            new XtfRevisionFeld("Distanz", null, "12.50")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel, new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Neu);

        var alle = Kanalschaeden(ziel);
        Assert.Equal(3, alle.Count);
        var neu = alle.Single(e => Kindwert(e, "KanalSchadencode") == "BAF");
        var tid = (string?)neu.Attribute("TID");
        Assert.False(string.IsNullOrWhiteSpace(tid));
        Assert.NotEqual("ch1000A1", tid);
        Assert.NotEqual("ch1000A2", tid);
        Assert.Equal("12.50", Kindwert(neu, "Distanz"));
        Assert.Equal("20260813", Kindwert(neu, "Letzte_Aenderung"));
        Assert.Equal(
            "U1",
            (string?)neu.Elements().First(e => e.Name.LocalName == "UntersuchungRef").Attribute("REF"));
    }

    [Fact]
    public void Das_Original_darf_nicht_ueberschrieben_werden()
    {
        var (quelle, _) = Dateien();

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, Plan(), quelle);

        Assert.False(ergebnis.Ok);
        Assert.Contains("Original", ergebnis.Fehler!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eine_vorhandene_Zieldatei_wird_nicht_ueberschrieben()
    {
        var (quelle, ziel) = Dateien();
        File.WriteAllText(ziel, "schon da");

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, Plan(), ziel);

        Assert.False(ergebnis.Ok);
        Assert.Equal("schon da", File.ReadAllText(ziel));
    }

    [Fact]
    public void Ein_Plan_mit_offenen_Faellen_wird_nicht_geschrieben()
    {
        var (quelle, ziel) = Dateien();
        var plan = new XtfRevisionPlan("original.xtf", Array.Empty<XtfRevisionPosition>(), new[] { "unklar" });

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    // Fail-closed: Was der Plan verlangt, muss angewandt worden sein. Sonst entstuende eine
    // Datei, die wie eine Revision aussieht und keine ist — genau der Fall vom 2026-08-13.
    [Fact]
    public void Ein_nicht_anwendbarer_Plan_schreibt_nichts()
    {
        var (quelle, ziel) = Dateien();
        var plan = new XtfRevisionPlan(
            "original.xtf",
            new[] { new XtfRevisionPosition(XtfRevisionAenderung.Neu, null, "GIBT-ES-NICHT", "99-999", "BAF", null, Array.Empty<XtfRevisionFeld>()) },
            Array.Empty<string>());

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
        Assert.Contains("nicht vollstaendig", ergebnis.Fehler!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eine_unbekannte_Kennung_bei_Aenderung_schreibt_ebenfalls_nichts()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000UNBEKANNT", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    [Fact]
    public void Eine_fehlende_Originaldatei_meldet_einen_Fehler_statt_zu_werfen()
    {
        var ergebnis = XtfRevisionWriter.Schreibe(
            Path.Combine(_dir, "gibt-es-nicht.xtf"),
            Plan(),
            Path.Combine(_dir, "ziel.xtf"));

        Assert.False(ergebnis.Ok);
        Assert.NotNull(ergebnis.Fehler);
    }

    [Fact]
    public void Ein_Plan_ohne_Aenderung_erzeugt_eine_inhaltsgleiche_Revision()
    {
        var (quelle, ziel) = Dateien();

        Assert.True(XtfRevisionWriter.Schreibe(quelle, Plan(), ziel).Ok);

        Assert.Equal(Kanalschaeden(quelle).Count, Kanalschaeden(ziel).Count);
        Assert.Equal("BAB", Kindwert(Kanalschaden(ziel, "ch1000A1"), "KanalSchadencode"));
    }

    // ── Hilfen ──────────────────────────────────────────────────────────

    private (string Quelle, string Ziel) Dateien()
    {
        var quelle = Path.Combine(_dir, "original.xtf");
        File.WriteAllText(quelle, Original);
        return (quelle, Path.Combine(_dir, "revision.xtf"));
    }

    private static XtfRevisionPlan Plan(params XtfRevisionPosition[] positionen)
        => new("original.xtf", positionen, Array.Empty<string>());

    private static XtfRevisionPosition Position(
        XtfRevisionAenderung art,
        string? tid,
        string code,
        params XtfRevisionFeld[] felder)
        => new(art, tid, "U1", "06-001", code, null, felder);

    private static List<XElement> Kanalschaeden(string pfad)
        => XDocument.Load(pfad).Descendants()
            .Where(e => e.Name.LocalName.EndsWith("Kanalschaden", StringComparison.Ordinal))
            .ToList();

    private static XElement? KanalschadenOderNull(string pfad, string tid)
        => Kanalschaeden(pfad).FirstOrDefault(e => (string?)e.Attribute("TID") == tid);

    private static XElement Kanalschaden(string pfad, string tid)
        => KanalschadenOderNull(pfad, tid) ?? throw new InvalidOperationException($"Kanalschaden {tid} fehlt.");

    private static string? Kindwert(XElement node, string name)
        => node.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
}
