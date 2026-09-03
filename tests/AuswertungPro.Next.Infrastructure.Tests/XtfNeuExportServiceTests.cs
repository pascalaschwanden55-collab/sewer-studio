using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfNeuExportServiceTests
{
    [Fact]
    public void Der_Bericht_beschreibt_die_tatsaechliche_Herkunft_der_Verwaltungsangaben()
    {
        var projekt = new Project { Name = "Seilergasse" };
        projekt.Data.Add(Haltung());

        var ergebnis = new XtfNeuExportService().Erzeuge(
            new XtfNeuExportRequest(projekt, "", NurPruefen: true));

        Assert.True(ergebnis.Ok);
        Assert.Contains("kommen aus ihren Projektfeldern", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.DoesNotContain("tragen den Eigentuemer", ergebnis.Bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Bericht_meldet_dass_Qgis_Objekte_trotzdem_geschrieben_werden()
    {
        var projekt = new Project { Name = "Seilergasse" };
        projekt.Data.Add(Haltung());
        var schacht = Schacht("78998");
        schacht.SetFieldValue(FieldKeys.CadastreObjectId, "ch1000a00000c03e", FieldSource.Kataster, false);
        projekt.SchaechteData.Add(schacht);

        var ergebnis = new XtfNeuExportService().Erzeuge(
            new XtfNeuExportRequest(projekt, "", NurPruefen: true));

        Assert.True(ergebnis.Ok);
        Assert.Contains("1 Objekt hat eine Objekt-ID und wird trotzdem geschrieben", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.Contains("Duplikate", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.Contains("Revidierte XTF", ergebnis.Bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Seilergasse_mit_Qgis_Objekt_ID_besteht_die_Pruefung()
    {
        var projekt = new Project { Name = "Seilergasse" };
        var haltung = Haltung();
        haltung.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);
        haltung.SetFieldValue("Schacht_oben", "78998", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_unten", "79002", FieldSource.Manual, true);
        projekt.Data.Add(haltung);
        projekt.SchaechteData.Add(Schacht("78998"));

        var ergebnis = new XtfNeuExportService().Erzeuge(
            new XtfNeuExportRequest(projekt, "", NurPruefen: true));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Null(ergebnis.Fehler);
        Assert.Contains("In die Datei: 1 Haltungen, 1 Schaechte", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.Contains("Objekt-ID", ergebnis.Bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Seilergasse_mit_Qgis_Objekt_ID_erzeugt_eine_Xtf_Datei()
    {
        var ziel = Path.Combine(Path.GetTempPath(), "SewerStudio_XtfNeu_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ziel);
        try
        {
            var projekt = new Project { Name = "Seilergasse Test export" };
            var haltung = Haltung();
            haltung.SetFieldValue(FieldKeys.CadastreObjectId, "100143", FieldSource.Kataster, false);
            haltung.SetFieldValue("Schacht_oben", "78998", FieldSource.Manual, true);
            haltung.SetFieldValue("Schacht_unten", "79002", FieldSource.Manual, true);
            projekt.Data.Add(haltung);
            projekt.SchaechteData.Add(Schacht("78998"));

            var ergebnis = new XtfNeuExportService().Erzeuge(new XtfNeuExportRequest(projekt, ziel));

            Assert.True(ergebnis.Ok, ergebnis.Fehler);
            Assert.NotNull(ergebnis.Datei);
            Assert.True(File.Exists(ergebnis.Datei));
            var inhalt = File.ReadAllText(ergebnis.Datei);
            Assert.Contains(".Haltung TID=", inhalt, StringComparison.Ordinal);
            Assert.Contains(".Normschacht TID=", inhalt, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(ziel, recursive: true);
        }
    }

    [Fact]
    public void Ohne_Katasterbezug_bleibt_die_Meldung_fuer_fehlende_Pflichtangaben()
    {
        var projekt = new Project { Name = "Unvollstaendig" };
        var haltung = Haltung();
        haltung.SetFieldValue(FieldKeys.Owner, "", FieldSource.Manual, true);
        projekt.Data.Add(haltung);

        var ergebnis = new XtfNeuExportService().Erzeuge(
            new XtfNeuExportRequest(projekt, "", NurPruefen: true));

        Assert.False(ergebnis.Ok);
        Assert.Contains("Pflichtangaben", ergebnis.Fehler, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", ergebnis.Fehler, StringComparison.Ordinal);
    }

    private static HaltungRecord Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "78998-79002", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, true);
        return record;
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        return record;
    }
}
