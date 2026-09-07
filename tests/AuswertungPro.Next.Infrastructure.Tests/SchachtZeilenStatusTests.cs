using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 6: Die Schachtliste bekommt denselben nur lesenden Protokollknopf wie
/// die Haltungsliste. Ob ein Schacht ein Protokoll hat, entscheidet dieselbe WPF-freie Regel —
/// und zwar ueber <see cref="SchachtFeldnamen"/>: Schachtfelder heissen nach der Kopfzeile der
/// Excel-Vorlage, ein direkter Katalogname findet sie nicht immer.
/// </summary>
public sealed class SchachtZeilenStatusTests
{
    [Theory]
    [InlineData(FieldKeys.PdfPath)]
    [InlineData(FieldKeys.PdfEigen)]
    [InlineData(FieldKeys.PdfAll)]
    public void Ein_gefuelltes_Protokollfeld_genuegt(string feld)
    {
        var record = new SchachtRecord();
        record.SetFieldValue(feld, @"D:\Projekt\Schaechte\78998.pdf", FieldSource.Manual, userEdited: false);

        Assert.True(SchachtZeilenStatus.HatProtokoll(record));
    }

    [Fact]
    public void Ohne_Protokollfeld_gibt_es_kein_Protokoll()
    {
        var record = new SchachtRecord();
        Assert.False(SchachtZeilenStatus.HatProtokoll(record));

        record.SetFieldValue(FieldKeys.PdfPath, "   ", FieldSource.Manual, userEdited: false);
        Assert.False(SchachtZeilenStatus.HatProtokoll(record));
    }

    /// <summary>
    /// Der Datensatz fuehrt das Feld unter einer abweichenden Schreibweise (Excel-Kopfzeile).
    /// Ein reiner Ordinalvergleich haette den Knopf still ausgeblendet.
    /// </summary>
    [Fact]
    public void Eine_abweichende_Schreibweise_wird_gefunden()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("PDF Path", @"D:\Projekt\Schaechte\78998.pdf", FieldSource.Manual, userEdited: false);

        Assert.True(SchachtZeilenStatus.HatProtokoll(record));
    }
}
