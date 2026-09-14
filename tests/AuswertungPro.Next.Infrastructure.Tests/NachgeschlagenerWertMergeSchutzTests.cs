using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Handkorrekturen bleiben unabhängig von der Herkunft geschützt.
/// Seit dem GeoShop-Feldvergleich vom 14.09.2026 sind auch bestätigte Katasterwerte
/// ohne Handmarkierung vor späteren Importen geschützt. Andere Nachschlagquellen
/// benötigen weiterhin die ausdrückliche Handmarkierung.
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
    public void Bestaetigter_Katasterwert_ist_auch_ohne_Handmarkierung_geschuetzt()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        var ergebnis = schacht.SetFieldValue("Funktion", "Etwas anderes", FieldSource.Xtf, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.KatasterwertGeschuetzt, ergebnis);
        Assert.Equal("Schlammsammler", schacht.GetFieldValue("Funktion"));
        Assert.False(schacht.IsUserEdited("Funktion"));
        Assert.NotNull(schacht.FieldMeta["Funktion"].Conflict);
    }

    [Fact]
    public void Andere_Nachschlagquelle_bleibt_ohne_Handmarkierung_ungeschuetzt()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Eigentuemer", "Muster, Hans", FieldSource.Grundbuch, userEdited: false);
        schacht.SetFieldValue("Eigentuemer", "Fremd, Egon", FieldSource.Pdf, userEdited: false);
        Assert.Equal("Fremd, Egon", schacht.GetFieldValue("Eigentuemer"));
    }

    [Fact]
    public void Haltungsmerge_meldet_Katasterabweichung_als_Konflikt_und_keine_Aenderung()
    {
        var ziel = new HaltungRecord(); var quelle = new HaltungRecord();
        ziel.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Kataster, false);
        quelle.SetFieldValue(FieldKeys.PipeMaterial, "Kunststoff", FieldSource.Pdf, false);
        var ergebnis = AuswertungPro.Next.Infrastructure.Import.Common.MergeEngine.MergeRecord(ziel, quelle, FieldSource.Pdf);
        Assert.Equal("Beton", ziel.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.Equal(0, ergebnis.Updated);
        Assert.Equal(1, ergebnis.Conflicts);
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
