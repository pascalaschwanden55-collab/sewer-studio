using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Projektregel «Handwerte, auch bewusst leer, ueberschreibt kein Import» ueber Schreibweisen
/// hinweg (Entscheid E3, 02.10.2026): Traegt eine Schreibweise eines Schachtfelds einen Handwert,
/// schreibt keine automatische Quelle in irgendeine Schreibweise derselben Gruppe. Vorher fuellte
/// der Import die ungeschuetzten Schreibweisen (Nachtrag Review PR #78).
/// Die Regel steht an EINER Stelle (<see cref="SchachtFeldnamen.HatHandwert"/>), der Schreibweg
/// des Datensatzes nutzt sie - damit gilt sie fuer XTF, SchachtPro, KINS, Kataster und PDF.
/// </summary>
public sealed class SchachtHandwertGruppeTests
{
    [Fact]
    public void Automatische_Quelle_schreibt_nicht_in_eine_andere_Schreibweise_eines_Handwerts()
    {
        // Wie der XTF-Import (Eigentuemer) gegen die Tabellenspalte (Eigentümer).
        var record = new SchachtRecord();
        record.SetFieldValue("Eigentümer", "Gemeinde", FieldSource.Manual, userEdited: true);

        var ergebnis = record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Xtf405, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt, ergebnis);
        Assert.Equal("", record.GetFieldValue(FieldKeys.Owner));
        Assert.Equal("Gemeinde", record.GetFieldValue("Eigentümer"));
    }

    [Fact]
    public void Bewusst_leere_Schreibweise_sperrt_das_Fuellen_der_ganzen_Gruppe()
    {
        // Wie KINS, QGIS und GeoShop: nur leere Felder fuellen.
        var record = new SchachtRecord();
        record.SetFieldValue("Ausführung Datum/Jahr", "", FieldSource.Manual, userEdited: true);

        Assert.False(record.FuelleLeeresFeld("Ausfuehrung Datum/Jahr", "2020", FieldSource.Kataster));
        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt,
            record.SetFieldValue("Ausfuehrung Datum/Jahr", "2020"));
        Assert.Equal("", record.GetFieldValue("Ausfuehrung Datum/Jahr"));
    }

    [Fact]
    public void Handeingabe_und_andere_Felder_bleiben_frei()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Eigentümer", "Gemeinde", FieldSource.Manual, userEdited: true);

        Assert.Equal(FeldSchreibErgebnis.Geschrieben,
            record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, userEdited: true));
        Assert.Equal(FeldSchreibErgebnis.Geschrieben,
            record.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Pdf, userEdited: false));
        Assert.True(record.FuelleLeeresFeld("Material", "Beton", FieldSource.Pdf));
    }

    [Fact]
    public void Pdf_Import_schreibt_in_keine_Schreibweise_einer_Gruppe_mit_Handwert()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Primäre Schäden", "Handkorrektur", FieldSource.Manual, userEdited: true);
        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht", "Rund", "1000 mm", "2.35", "BAC Import", null, "offen", null);

        SchachtProtocolApplier.Apply(record, "74467", parsed, Array.Empty<(string, string)>(), "C:/x/quelle.pdf");

        Assert.Equal("Handkorrektur", record.GetFieldValue("Primäre Schäden"));
        Assert.Equal("", record.GetFieldValue("Primaere Schaeden"));
        Assert.Equal("", record.GetFieldValue("Prim\u00c3\u00a4re Sch\u00c3\u00a4den"));
        // Andere Felder kommen weiter an.
        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Pdf_Ergaenzung_beachtet_bewusst_leer_in_einer_Schreibweise()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Ausführung Datum/Jahr", "", FieldSource.Manual, userEdited: true);
        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(record, "74467", parsed, Array.Empty<(string, string)>(), "C:/x/quelle.pdf",
            fillMissingOnly: true);

        Assert.Equal("", record.GetFieldValue("Ausfuehrung Datum/Jahr"));
        Assert.Equal("", record.GetFieldValue("Ausf\u00c3\u00bchrung Datum/Jahr"));
        Assert.True(record.IstBewusstLeer("Ausführung Datum/Jahr"));
    }

    [Fact]
    public void Masse_ergaenzen_kein_halbes_Paar_wenn_eine_Schreibweise_bewusst_leer_ist()
    {
        // Die Tabelle fuehrt das Mass unter "Dimension 1 mm", der Datensatz zusaetzlich unter
        // "Dimension1_mm" - bewusst leer. Dann bleibt das ganze Paar offen.
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "", FieldSource.Legacy, userEdited: false);
        record.SetFieldValue("Dimension1_mm", "", FieldSource.Manual, userEdited: true);

        var geschrieben = SchachtMasse.Schreibe(record, ("1000", "800"), FieldSource.Pdf, userEdited: false, nurLeere: true);

        Assert.False(geschrieben);
        Assert.Equal("", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("", record.GetFieldValue(FieldKeys.ShaftDimension2Mm));
    }

    // Review PR #80 (P1): bekannte Mojibake-Schreibweisen gehoeren zur selben Gruppe.
    private const string PrimaereMojibake = "Prim\u00c3\u00a4re Sch\u00c3\u00a4den";

    [Fact]
    public void Handwert_unter_Mojibake_Schreibweise_sperrt_den_kanonischen_Namen()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(PrimaereMojibake, "Handkorrektur", FieldSource.Manual, userEdited: true);

        Assert.True(SchachtFeldnamen.HatHandwert(record, "Primaere_Schaeden"));
        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt,
            record.SetFieldValue("Primäre Schäden", "Import", FieldSource.Xtf405, userEdited: false));
        Assert.Contains(PrimaereMojibake, SchachtFeldnamen.Schreibweisen(record, "Primäre Schäden"));
        Assert.True(AuswertungPro.Next.Application.Xtf.XtfSchachtPlanBuilder.IstHandgesetzt(record, "Primäre Schäden"));
    }

    // Review PR #80 (P2): Altbestand mit bewusst leerem Handwert in A und aelterem Importwert in B.
    private static SchachtRecord AltbestandBewusstLeerNebenImport()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "74467", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue("Ausfuehrung Datum/Jahr", "01.01.2020", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue("Ausführung Datum/Jahr", "", FieldSource.Manual, userEdited: true);
        return record;
    }

    [Fact]
    public void Exportleser_liefert_den_bewusst_leeren_Handwert_statt_des_Importwerts()
    {
        var record = AltbestandBewusstLeerNebenImport();

        Assert.Equal("", AuswertungPro.Next.Application.Xtf.XtfSchachtPlanBuilder.Wert(record, "Ausführung Datum/Jahr"));
        Assert.Equal("", AuswertungPro.Next.Application.Xtf.XtfSchachtPlanBuilder.Wert(record, "Ausfuehrung Datum/Jahr"));
    }

    [Fact]
    public void Neuaufbau_leert_die_Importschreibweise_und_laesst_den_Handwert_stehen()
    {
        var record = AltbestandBewusstLeerNebenImport();
        var ohneDatum = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, "Kontrollschacht", null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(record, "74467", ohneDatum, Array.Empty<(string, string)>(), "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.Equal("", record.GetFieldValue("Ausfuehrung Datum/Jahr"));
        Assert.True(record.IstBewusstLeer("Ausführung Datum/Jahr"));
        Assert.False(record.IsUserEdited("Ausfuehrung Datum/Jahr"));
    }

    [Fact]
    public void Ohne_Handwert_leert_der_Neuaufbau_wie_bisher()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Ausfuehrung Datum/Jahr", "01.01.2020", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue("Ausführung Datum/Jahr", "01.01.2020", FieldSource.Pdf, userEdited: false);
        var ohneDatum = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", null, null, null, null, null, null, null, null, null);

        SchachtProtocolApplier.Apply(record, "74467", ohneDatum, Array.Empty<(string, string)>(), "C:/x/neu.pdf",
            rebuildFromProtocol: true);

        Assert.Equal("", record.GetFieldValue("Ausfuehrung Datum/Jahr"));
        Assert.Equal("", record.GetFieldValue("Ausführung Datum/Jahr"));
    }
}
