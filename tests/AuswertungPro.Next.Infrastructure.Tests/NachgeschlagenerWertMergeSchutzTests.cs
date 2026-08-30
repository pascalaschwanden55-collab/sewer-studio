using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Schutz eines nachgeschlagenen Werts haengt AUSSCHLIESSLICH an
/// userEdited: true.
///
/// Die naheliegende Gegenannahme ist falsch: Eine niedrige Merge-Prioritaet
/// schuetzt NICHT. MergeEngine entscheidet mit
/// "GetPriority(import) > GetPriority(bestehend)" zugunsten der HOEHEREN
/// Zahl, und neue Herkuenfte bekommen ueber den Fall-through die 0 — sie
/// verlieren damit gegen jeden Import. Was wirklich schuetzt, ist die
/// Handwert-Regel, die vor jeder Prioritaetsrechnung greift.
///
/// Die beiden letzten Tests zusammen belegen das. Ohne den Gegentest saehe
/// die Absicherung staerker aus, als sie ist.
/// </summary>
public sealed class NachgeschlagenerWertMergeSchutzTests
{
    [Fact]
    public void Die_neuen_Herkuenfte_existieren()
    {
        Assert.True(Enum.IsDefined(FieldSource.Kataster));
        Assert.True(Enum.IsDefined(FieldSource.Grundbuch));
    }

    [Fact]
    public void Mit_userEdited_ueberlebt_der_Wert_einen_Import()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: true);

        var ergebnis = schacht.SetFieldValue(
            "Funktion", "Etwas anderes", FieldSource.Xtf, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.HandwertGeschuetzt, ergebnis);
        Assert.Equal("Schlammsammler", schacht.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Ohne_userEdited_ist_derselbe_Wert_ungeschuetzt()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        schacht.SetFieldValue("Funktion", "Etwas anderes", FieldSource.Xtf, userEdited: false);

        // Genau deshalb ist userEdited: true beim Uebernehmen Pflicht.
        Assert.Equal("Etwas anderes", schacht.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Auch_ein_Grundbuchwert_ueberlebt_mit_userEdited()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Eigentuemer", "Muster, Hans", FieldSource.Grundbuch, userEdited: true);

        schacht.SetFieldValue("Eigentuemer", "Fremd, Egon", FieldSource.Pdf, userEdited: false);

        Assert.Equal("Muster, Hans", schacht.GetFieldValue("Eigentuemer"));
    }
}
