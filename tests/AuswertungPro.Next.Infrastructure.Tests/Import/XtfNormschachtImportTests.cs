using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Schaechte aus der SIA405-XTF.
///
/// Bis 2026-08-30 legte kein XTF-Weg Schaechte an: Der QGIS-Export Zone 1.17 enthaelt
/// 295 Normschacht-Objekte mit Funktion (100 %), Material (100 %), Dimension1/2 (98 %)
/// und Eigentuemer (289 von 295) — davon kam nichts in SewerStudio an. Gemessen an
/// allen 17 echten Projekten waren alle 122 vorhandenen Eigentumsangaben von Hand
/// gesetzt (FieldSource.Manual), keine einzige aus einem Import.
///
/// Uebernommen wird nur, was ausdruecklich in SewerStudio gebraucht wird: Nummer,
/// Funktion, Material, Dimension und Eigentuemer. Status, Sanierungsbedarf, Baujahr,
/// Sohlenkote, Lagebestimmung und die Deckelangaben bleiben bewusst draussen — sie sind
/// informativ und stehen im Protokoll.
/// </summary>
public sealed class XtfNormschachtImportTests : IDisposable
{
    private readonly string _dir;

    public XtfNormschachtImportTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"xtf-schacht-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    // Aufbau wie im echten QGIS-Export Zone 1.17.
    private const string Quelle = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="KA1">
        <Bezeichnung>82265-82331</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
        <Eigentuemer>Abwasser Uri</Eigentuemer>
      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="HA1">
        <Bezeichnung>82265-82331</Bezeichnung>
        <LaengeEffektiv>21.40</LaengeEffektiv>
        <Lichte_Hoehe>350</Lichte_Hoehe>
        <Material>Steinzeug</Material>
        <AbwasserbauwerkRef REF="KA1" />
      </SIA405_Abwasser.SIA405_Abwasser.Haltung>
      <SIA405_Abwasser.SIA405_Abwasser.Normschacht TID="NS1">
        <Bezeichnung>82099</Bezeichnung>
        <Funktion>Kontrollschacht</Funktion>
        <Material>Beton</Material>
        <Dimension1>600</Dimension1>
        <Dimension2>600</Dimension2>
        <Status>in_Betrieb</Status>
        <Sanierungsbedarf>unbekannt</Sanierungsbedarf>
        <Baujahr>1975</Baujahr>
        <Eigentuemer>Privat</Eigentuemer>
      </SIA405_Abwasser.SIA405_Abwasser.Normschacht>
      <SIA405_Abwasser.SIA405_Abwasser.Normschacht TID="NS2">
        <Bezeichnung>82265</Bezeichnung>
        <Funktion>Schlammsammler</Funktion>
        <Material>unbekannt</Material>
        <Dimension1>1100</Dimension1>
        <Dimension2>900</Dimension2>
        <Bemerkung>Tauchbogen fehlt</Bemerkung>
        <Eigentuemer>Abwasser Uri</Eigentuemer>
      </SIA405_Abwasser.SIA405_Abwasser.Normschacht>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    private Project Importiere(string inhalt = Quelle)
    {
        var pfad = Path.Combine(_dir, $"q{Guid.NewGuid():N}.xtf");
        File.WriteAllText(pfad, inhalt);
        var projekt = new Project();
        var stats = new LegacyXtfImportService().ImportXtfFiles([pfad], projekt);
        Assert.True(stats.Errors == 0,
            string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message}")));
        return projekt;
    }

    private static SchachtRecord Schacht(Project p, string nummer)
        => Assert.Single(p.SchaechteData, s => s.GetFieldValue("Schachtnummer") == nummer);

    [Fact]
    public void Die_Normschaechte_werden_angelegt()
    {
        var p = Importiere();

        Assert.Equal(2, p.SchaechteData.Count);
        Assert.Equal(
            new[] { "82099", "82265" },
            p.SchaechteData.Select(s => s.GetFieldValue("Schachtnummer")).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Die_Nummer_steht_nur_im_Feld_Schachtnummer()
    {
        // Der SchachtPro-Import fuellt zusaetzlich "NR." und "Nr." mit der Schachtnummer.
        // Das darf hier NICHT nachgeahmt werden: Gemessen an den 17 echten Projekten
        // tragen diese beiden Felder bei 257 von 257 Schaechten eine LAUFENDE Nummer
        // (1, 2, 3 ...) und in keinem einzigen Fall die Schachtnummer. Ein Import wuerde
        // dort die Durchnummerierung ueberschreiben.
        var s = Schacht(Importiere(), "82099");

        Assert.Equal("82099", s.GetFieldValue("Schachtnummer"));
        Assert.True(string.IsNullOrEmpty(s.GetFieldValue("NR.")));
        Assert.True(string.IsNullOrEmpty(s.GetFieldValue("Nr.")));
    }

    [Fact]
    public void Eine_vorhandene_laufende_Nummer_bleibt_unberuehrt()
    {
        // Der praktische Fall: Der Schacht kam aus einem PDF-Import mit NR. = 7 und wird
        // jetzt aus der XTF ergaenzt. Die 7 muss die 7 bleiben.
        var pfad = Path.Combine(_dir, "laufend.xtf");
        File.WriteAllText(pfad, Quelle);
        var projekt = new Project();
        var vorhanden = new SchachtRecord();
        vorhanden.SetFieldValue("Schachtnummer", "82099", FieldSource.Pdf, userEdited: false);
        vorhanden.SetFieldValue("NR.", "7", FieldSource.Pdf, userEdited: false);
        projekt.SchaechteData.Add(vorhanden);

        new LegacyXtfImportService().ImportXtfFiles([pfad], projekt);

        Assert.Equal("7", Schacht(projekt, "82099").GetFieldValue("NR."));
        Assert.Equal("Privat", Schacht(projekt, "82099").GetFieldValue(FieldKeys.Owner));
        // Und kein zweiter Datensatz fuer denselben Schacht.
        Assert.Equal(2, projekt.SchaechteData.Count);
    }

    [Fact]
    public void Das_Eigentum_kommt_im_amtlichen_Begriff_an()
    {
        var p = Importiere();

        // Der amtliche Begriff bleibt stehen; die Excel-Vorlage faerbt ihn seit
        // 2026-08-31 selbst. Frueher wurde hier auf die Kurzform "AWU"
        // umgeschrieben, weil nur sie gefaerbt wurde.
        Assert.Equal("Abwasser Uri", Schacht(p, "82265").GetFieldValue(FieldKeys.Owner));
        Assert.Equal("Privat", Schacht(p, "82099").GetFieldValue(FieldKeys.Owner));

        // Dieselbe Regel muss fuer die Haltung gelten - der Import schrieb dort einmal
        // einen unbehandelten Rohwert.
        var haltung = Assert.Single(p.Data);
        Assert.Equal("Abwasser Uri", haltung.GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Ein_runder_Schacht_bekommt_ein_Mass_ein_eckiger_zwei()
    {
        var p = Importiere();

        // Die Masse leben in zwei Zahlenfeldern: rund = beide gleich, eckig = zwei Werte.
        Assert.Equal("600", Schacht(p, "82099").GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("600", Schacht(p, "82099").GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal("1100", Schacht(p, "82265").GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("900", Schacht(p, "82265").GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.False(Schacht(p, "82099").Fields.ContainsKey("Dimension"));
    }

    [Fact]
    public void Funktion_und_Material_kommen_an_unbekannt_aber_nicht()
    {
        var p = Importiere();

        Assert.Equal("Kontrollschacht", Schacht(p, "82099").GetFieldValue("Funktion"));
        Assert.Equal("Beton", Schacht(p, "82099").GetFieldValue("Material"));

        // "unbekannt" ist keine Angabe. Im Kantonsexport steht es bei 211 von 295
        // Schaechten - es wuerde die Spalte fuellen, ohne etwas zu sagen. Dieselbe
        // Regel wendet der Kanal-Import schon auf die Zugaenglichkeit an.
        Assert.True(string.IsNullOrEmpty(Schacht(p, "82265").GetFieldValue("Material")));
        Assert.Equal("Schlammsammler", Schacht(p, "82265").GetFieldValue("Funktion"));
    }

    [Fact]
    public void Status_Baujahr_und_Bemerkung_kommen_an_die_Deckelangaben_nicht()
    {
        // Bis 2026-09-03 blieben Status, Sanierungsbedarf, Baujahr und Bemerkung draussen.
        // Die Rundreise Export, Import, Vergleich zeigte: Was SewerStudio selbst
        // hinausschreibt, kam nicht zurueck. "unbekannt" bleibt weiterhin keine Angabe.
        var p = Importiere();
        var s = Schacht(p, "82099");
        Assert.Equal("in_Betrieb", s.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Equal("1975", s.GetFieldValue(FieldKeys.ConstructionYear));
        Assert.True(string.IsNullOrEmpty(s.GetFieldValue(FieldKeys.RehabilitationNeed)), "unbekannt ist keine Angabe");
        Assert.Equal("Tauchbogen fehlt", Schacht(p, "82265").GetFieldValue(FieldKeys.Remarks));

        foreach (var feld in new[] { "Sohlenkote", "Deckelmaterial", "Deckelform", "Deckeldurchmesser", "Steighilfe" })
            Assert.True(string.IsNullOrEmpty(s.GetFieldValue(feld)), $"{feld} sollte leer bleiben");
    }

    [Fact]
    public void Ein_zweiter_Import_derselben_Datei_erzeugt_keine_Dublette()
    {
        var pfad = Path.Combine(_dir, "doppelt.xtf");
        File.WriteAllText(pfad, Quelle);
        var projekt = new Project();
        var dienst = new LegacyXtfImportService();

        dienst.ImportXtfFiles([pfad], projekt);
        dienst.ImportXtfFiles([pfad], projekt);

        Assert.Equal(2, projekt.SchaechteData.Count);
    }

    [Fact]
    public void Eine_Handaenderung_ueberlebt_den_erneuten_Import()
    {
        // Die Grundregel: Was von Hand korrigiert wurde, bleibt - auch wenn dieselbe
        // Datei versehentlich nochmals importiert wird.
        var pfad = Path.Combine(_dir, "hand.xtf");
        File.WriteAllText(pfad, Quelle);
        var projekt = new Project();
        var dienst = new LegacyXtfImportService();

        dienst.ImportXtfFiles([pfad], projekt);
        var s = Schacht(projekt, "82099");
        s.SetFieldValue("Material", "Kunststoff", FieldSource.Manual, userEdited: true);
        s.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);

        dienst.ImportXtfFiles([pfad], projekt);

        Assert.Equal("Kunststoff", Schacht(projekt, "82099").GetFieldValue("Material"));
        Assert.Equal("Gemeinde", Schacht(projekt, "82099").GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Ein_Schacht_ohne_Bezeichnung_wird_uebersprungen()
    {
        var ohne = Quelle.Replace("<Bezeichnung>82099</Bezeichnung>", "<Bezeichnung></Bezeichnung>");
        var p = Importiere(ohne);

        // Ohne Nummer laesst sich der Schacht spaeter keinem Protokoll zuordnen.
        Assert.Equal("82265", Assert.Single(p.SchaechteData).GetFieldValue("Schachtnummer"));
    }
}
