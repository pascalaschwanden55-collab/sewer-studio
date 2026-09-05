using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Der Bericht des Neu-Exports, wenn Bauteile ihre GEONIS-Kennungen tragen.</summary>
public sealed class XtfNeuExportServiceGeonisTests
{
    [Fact]
    public void Der_Bericht_zaehlt_die_Objekte_mit_GEONIS_Kennung_und_nennt_das_abweichende_Profil()
    {
        var projekt = new Project { Name = "Seilergasse" };
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "78998-79002", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_oben", "78998", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_unten", "79002", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);
        haltung.Geonis = new GeonisKennungen
        {
            Haltung = "ch23h1a4uL3A2Sjp",
            Kanal = "ch23h1a46oVbkGmT",
            Rohrprofil = "ch23h1a43obhLa8B",
            RohrprofilTyp = "unbekannt"
        };
        projekt.Data.Add(haltung);

        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "78998", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        schacht.Geonis = new GeonisKennungen { Knoten = "ch23h1a4ftlGdbHU", Bauwerk = "ch23h1a4Umcgr2UF" };
        projekt.SchaechteData.Add(schacht);

        var ergebnis = new XtfNeuExportService().Erzeuge(
            new XtfNeuExportRequest(projekt, "", NurPruefen: true));

        Assert.True(ergebnis.Ok);
        Assert.Contains("2 Objekte tragen ihre GEONIS-Kennung aus dem Kataster", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.Contains("Rohrprofil weicht vom Kataster ab", ergebnis.Bericht, StringComparison.Ordinal);
        // Die Objekt-ID-Warnung gilt nur ohne GEONIS-Kennung; mit ihr waere sie falsch.
        Assert.DoesNotContain("wird trotzdem geschrieben", ergebnis.Bericht, StringComparison.Ordinal);
        Assert.DoesNotContain("GEONIS-Kennung ch23h1a4uL3A2Sjp aus dem Kataster verwendet", ergebnis.Bericht, StringComparison.Ordinal);
    }
}
