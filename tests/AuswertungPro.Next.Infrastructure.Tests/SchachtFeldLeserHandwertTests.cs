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
}
