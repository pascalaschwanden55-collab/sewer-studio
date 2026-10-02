using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Application.UseCases.ProjektPruefung;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Folgepaket zu PR #80: Reine Leser von Schachtfeldern lesen ueber
/// <see cref="SchachtFeldnamen.Wert"/> — Handwert (auch bewusst leer) vor Importwert, darin der
/// juengste. Vorher lasen sie ueber <see cref="SchachtFeldnamen.Feld"/> die erste Schreibweise
/// mit Inhalt und zeigten so einen alten Importwert neben einer bewusst geleerten Handkorrektur.
/// Je Leser derselbe Altbestand: A = bewusst leer von Hand, B = aelterer Importwert.
/// </summary>
public sealed class SchachtFeldLeserHandwertTests
{
    /// <summary>Altbestand aus der Zeit vor der Gruppensperre (B zuerst, dann A von Hand).</summary>
    internal static SchachtRecord Altbestand(string feldA, string feldB, string importwert, SchachtRecord? record = null)
    {
        record ??= new SchachtRecord();
        if (string.IsNullOrEmpty(record.GetFieldValue("Schachtnummer")))
            record.SetFieldValue("Schachtnummer", "S-1", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(feldB, importwert, FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(feldA, "", FieldSource.Manual, userEdited: true);
        Assert.Equal(importwert, record.GetFieldValue(feldB));
        return record;
    }

    [Fact]
    public void Projektpruefung_meldet_keinen_alten_Importwert_neben_bewusst_leer()
    {
        var projekt = new Project();
        projekt.SchaechteData.Add(Altbestand(FieldKeys.UsageType, "NUTZUNGSART", "Unsinn"));

        var ergebnis = ProjektPruefregeln.Pruefe(projekt, _ => null);

        Assert.DoesNotContain(ergebnis.Punkte, p => p.Meldung.Contains("Unsinn", StringComparison.Ordinal));
    }

    [Fact]
    public void Xtf_Zusatzangaben_lesen_den_bewusst_leeren_Handwert()
    {
        var record = Altbestand(FieldKeys.RecommendedRehabilitationMeasures, "Empfohlene Sanierungsmassnahmen", "Ersatz");

        Assert.Equal("", XtfZusatzangaben.SchachtfeldWert(record, FieldKeys.RecommendedRehabilitationMeasures));
    }

    [Fact]
    public void Xtf_Zusatzangaben_lesen_ohne_Handwert_weiter_den_Ausweichnamen()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.RecommendedRehabilitationMeasures, "", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue("Massnahmen", "Ersatz", FieldSource.Pdf, userEdited: false);

        Assert.Equal("Ersatz", XtfZusatzangaben.SchachtfeldWert(record, FieldKeys.RecommendedRehabilitationMeasures));
    }

    [Fact]
    public void Schachtgrafik_zeigt_keine_alte_Schachtform_neben_bewusst_leer()
    {
        var record = Altbestand(FieldKeys.ShaftShape, "SCHACHTFORM", "Oval");

        var modell = SchachtgrafikModellBuilder.Baue(record, null, null, null, "#000000");

        Assert.Null(modell.Schachtform);
    }

    [Fact]
    public void Protokollquelle_nennt_keinen_alten_Pfad_neben_bewusst_leer()
    {
        var record = Altbestand(FieldKeys.PdfPath, "PDF Path", "C:/alt/protokoll.pdf");

        Assert.Empty(SchachtProtokollQuelle.Kandidaten(record));
    }

    [Fact]
    public void Objektakte_liest_das_Stammfeld_wie_der_Export()
    {
        var record = Altbestand(FieldKeys.ShaftShape, "SCHACHTFORM", "Oval");
        var projekt = new Project();
        projekt.SchaechteData.Add(record);
        var bearbeitung = new ObjektaktenBearbeitung(projekt, record.Id, "schacht");

        var wert = bearbeitung.Lies(new ObjektAkte { Id = record.Id, Art = "schacht" },
            FieldCatalog.Objektfelder.Feld("schacht.form"));

        Assert.Equal("", wert);
    }
}
