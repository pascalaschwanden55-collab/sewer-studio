using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Fehlende Bezuege im SIA405-Haltungsimport stehen im Importbericht (Auftrag Pascal
/// 01.10.2026, Befund 2 aus AP08). Bis dahin fielen ein Kanal-, Rohrprofil-,
/// Haltungspunkt- oder Organisationsverweis ins Leere und eine Haltung ohne Namen still
/// weg. Die Werte bleiben unveraendert; neu ist nur die Meldung. Ein Organisationsverweis
/// ausserhalb der Datei ist nach Norm erlaubt (EXTERNAL) und darum keine Warnung.
/// </summary>
public sealed class XtfSia405BezugsmeldungenTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_bezuege_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Theory]
    [InlineData("refHALTUNG3", "Kanalverweis refKANALFEHLT")]
    [InlineData("refHALTUNG3", "Rohrprofilverweis refPROFILFEHLT")]
    [InlineData("refHALTUNG3", "Haltungspunktverweis refPUNKTFEHLT (oben)")]
    [InlineData("refHALTUNG4", "Eigentümer-Verweis refORGOHNENAME")]
    [InlineData("refHALTUNG5", "Haltung ohne Bezeichnung")]
    public void Ein_fehlender_Bezug_steht_als_Warnung_mit_TID_im_Importbericht(string tid, string verweis)
    {
        var (_, stats) = Importiere("sia405-referenz.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains($"TID {tid}", StringComparison.Ordinal)
                                                         && m.Message.Contains(verweis, StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF405", meldung.Context);
    }

    [Fact]
    public void Ein_Profilverweis_ins_Leere_nennt_den_Rueckfall_auf_die_Lichte_Breite_und_aendert_keinen_Wert()
    {
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        Assert.Contains(stats.Messages, m => m.Message.Contains("refPROFILFEHLT", StringComparison.Ordinal)
                                             && m.Message.Contains("Lichte_Breite", StringComparison.Ordinal));
        var haltung = Assert.Single(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "102-103");
        Assert.Equal("500", haltung.GetFieldValue(FieldKeys.ClearWidthMm));
        Assert.Equal("", haltung.GetFieldValue("Schacht_oben"));
        Assert.DoesNotContain(projekt.Data, r => r.GetFieldValue(FieldKeys.CadastreObjectId) == "refHALTUNG5");
    }

    [Fact]
    public void Eine_Organisation_ausserhalb_der_Datei_ist_kein_Fehler_sondern_ein_Hinweis()
    {
        var (_, stats) = Importiere("sia405-referenz.xtf");

        Assert.DoesNotContain(stats.Messages, m => m.Level != "Info" && m.Message.Contains("refORGFEHLT", StringComparison.Ordinal));
        var hinweis = Assert.Single(stats.Messages, m => m.Message.Contains("refORGFEHLT", StringComparison.Ordinal));
        Assert.Contains("ausserhalb der Datei", hinweis.Message, StringComparison.Ordinal);
        Assert.Contains("Datenlieferant", hinweis.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Haltung_ohne_TID_steht_als_Warnung_im_Importbericht_und_bleibt_uebersprungen()
    {
        // Seit 01.10.2026 (zweite Runde): Der Leser ueberspringt sie wie bisher, aber nicht mehr still.
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("ohne-tid", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF405", meldung.Context);
        Assert.Equal("Haltung \"ohne-tid\" ohne TID nicht übernommen: Ohne Objektkennung lässt sie sich nicht eindeutig zuordnen.",
            meldung.Message);
        Assert.DoesNotContain(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "ohne-tid");
    }

    [Fact]
    public void Ein_Knotenverweis_ins_Leere_steht_als_Warnung_und_der_Schacht_bleibt_leer()
    {
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("refKNOTENFEHLT", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Equal("XTF405", meldung.Context);
        Assert.Equal("Haltung \"101-102\" (TID refHALTUNG2): Abwasserknotenverweis refKNOTENFEHLT am Haltungspunkt "
                     + "refPUNKT2NACH (unten) zeigt ins Leere – Schacht unten nicht übernommen.", meldung.Message);
        var haltung = Assert.Single(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "101-102");
        Assert.Equal("", haltung.GetFieldValue("Schacht_unten"));
    }

    [Fact]
    public void Ein_Knotenverweis_ins_Leere_nennt_den_unveraenderten_Rueckfall_auf_Punkt_oder_Haltungsnamen()
    {
        var (projekt, stats) = Importiere(Schreibe("knoten.xtf", Sia405Xtf("""
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="refPV">
                    <Bezeichnung>P-77</Bezeichnung>
                    <AbwassernetzelementRef REF="refKNOTENWEG" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="refPN">
                    <Bezeichnung>77-78_nach</Bezeichnung>
                    <AbwassernetzelementRef REF="refKNOTENWEG2" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="refPH">
                    <Bezeichnung>Anschluss-80</Bezeichnung>
                    <AbwassernetzelementRef REF="refH1" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="refH1">
                    <Bezeichnung>77-78</Bezeichnung>
                    <vonHaltungspunktRef REF="refPV" />
                    <nachHaltungspunktRef REF="refPN" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="refH2">
                    <Bezeichnung>80-81</Bezeichnung>
                    <nachHaltungspunktRef REF="refPH" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
            """)));

        Assert.Contains(stats.Messages, m => m.Level == "Warn" && m.Message ==
            "Haltung \"77-78\" (TID refH1): Abwasserknotenverweis refKNOTENWEG am Haltungspunkt refPV (oben) zeigt ins Leere – "
            + "Schacht oben ersatzweise aus dem Punktnamen übernommen (\"P-77\").");
        Assert.Contains(stats.Messages, m => m.Level == "Warn" && m.Message ==
            "Haltung \"77-78\" (TID refH1): Abwasserknotenverweis refKNOTENWEG2 am Haltungspunkt refPN (unten) zeigt ins Leere – "
            + "Schacht unten ersatzweise aus dem Haltungsnamen übernommen (\"78\").");
        // Ein Verweis auf eine Haltung der Datei (Anschluss an eine Leitung) zeigt nicht ins Leere.
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("refPH", StringComparison.Ordinal));

        var haltung = Assert.Single(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == "77-78");
        Assert.Equal("P-77", haltung.GetFieldValue("Schacht_oben"));
        Assert.Equal("78", haltung.GetFieldValue("Schacht_unten"));
    }

    [Fact]
    public void Ein_externer_Organisationsverweis_eines_Schachts_steht_im_selben_Hinweis_wie_die_der_Haltungen()
    {
        var (projekt, stats) = Importiere("sia405-referenz.xtf");

        var hinweis = Assert.Single(stats.Messages, m => m.Message.Contains("ausserhalb der Datei", StringComparison.Ordinal));
        Assert.Equal("Info", hinweis.Level);
        Assert.Equal("Organisationsverweise ausserhalb der Datei (nach Norm zulässig, Name nicht übernommen): "
                     + "Eigentümer refORGFEHLT (1 Schacht), Datenlieferant refORGFEHLT (1 Haltung).", hinweis.Message);
        var schacht = Assert.Single(projekt.SchaechteData, s => s.GetFieldValue("Schachtnummer") == "101");
        Assert.Equal("", schacht.GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Ein_Schacht_mit_Verweis_auf_eine_Organisation_ohne_Bezeichnung_steht_als_Warnung()
    {
        var (_, stats) = Importiere(Schreibe("schacht-org.xtf", Sia405Xtf("""
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="refS500">
                    <Bezeichnung>500</Bezeichnung>
                    <Funktion>Kontrollschacht</Funktion>
                    <EigentuemerRef REF="refORGEXT" />
                    <DatenherrRef REF="refORGLEER" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
                  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="refS501">
                    <Bezeichnung>501</Bezeichnung>
                    <Eigentuemer>Privat</Eigentuemer>
                    <EigentuemerRef REF="refORGLEER" />
                    <DatenlieferantRef REF="refORGEXT" />
                  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
            """, """
                <SIA405_Base_Abwasser_LV95.Administration BID="refB2">
                  <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="refORGLEER">
                    <Organisationstyp>Privat</Organisationstyp>
                  </SIA405_Base_Abwasser_LV95.Administration.Organisation>
                </SIA405_Base_Abwasser_LV95.Administration>
            """)));

        // Ein Eigentuemertext hat Vorrang vor dem Verweis; dann fehlt nichts (Schacht 501).
        var warnung = Assert.Single(stats.Messages, m => m.Level == "Warn");
        Assert.Equal("XTF405", warnung.Context);
        Assert.Equal("Normschacht \"500\" (TID refS500): Datenherr-Verweis refORGLEER zeigt auf eine Organisation ohne "
                     + "Bezeichnung – Datenherr nicht übernommen.", warnung.Message);
        var hinweis = Assert.Single(stats.Messages, m => m.Message.Contains("ausserhalb der Datei", StringComparison.Ordinal));
        Assert.EndsWith(": Eigentümer refORGEXT (1 Schacht), Datenlieferant refORGEXT (1 Schacht).", hinweis.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Vollstaendige_Bezuege_erzeugen_keine_Meldung()
    {
        var (_, stats) = Importiere("sia405-bezuege.xtf");

        Assert.DoesNotContain(stats.Messages, m => m.Level == "Warn");
        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("ausserhalb der Datei", StringComparison.Ordinal));
    }

    private string Schreibe(string name, string inhalt)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, name);
        File.WriteAllText(pfad, inhalt);
        return pfad;
    }

    private static string Sia405Xtf(string objekte, string weitereBaskets = "") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION VERSION="2.3" SENDER="Test">
            <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="refB1">
        {objekte}
            </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
        {weitereBaskets}
          </DATASECTION>
        </TRANSFER>
        """;

    private (Project Projekt, ImportStats Stats) Importiere(string datei)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.IsPathRooted(datei) ? datei : Path.Combine(_dir, datei);
        if (!Path.IsPathRooted(datei))
            File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", datei), pfad);

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        return (projekt, stats);
    }
}
