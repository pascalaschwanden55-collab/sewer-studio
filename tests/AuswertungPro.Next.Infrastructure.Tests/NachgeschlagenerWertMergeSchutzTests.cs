using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Handkorrekturen bleiben unabhängig von der Herkunft geschützt.
/// Entscheid Pascal 23.09.2026 abends (ersetzt die Regel vom 14.09.2026): Die Daten der Kanalfirma
/// sind der Ist-Zustand — ein Import der Kanalfirma ersetzt Werte aus GeoShop, QGIS oder WebGIS
/// (Herkunft Kataster), eine Handkorrektur nie. Nur eine unbekannte Herkunft ersetzt keinen Katasterwert.
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

    [Theory]
    [InlineData(FieldSource.Xtf)]
    [InlineData(FieldSource.Xtf405)]
    [InlineData(FieldSource.Legacy)]
    [InlineData(FieldSource.Pdf)]
    [InlineData(FieldSource.Spro)]
    public void Import_der_kanalfirma_ersetzt_einen_katasterwert(FieldSource quelle)
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        var ergebnis = schacht.SetFieldValue("Funktion", "Etwas anderes", quelle, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.Geschrieben, ergebnis);
        Assert.Equal("Etwas anderes", schacht.GetFieldValue("Funktion"));
        Assert.Equal(quelle, schacht.FieldMeta["Funktion"].Source);
    }

    [Fact]
    public void Kanalfirma_ueber_den_einfachen_schreibweg_ersetzt_einen_katasterwert()
    {
        // KINS-Anreicherung und andere Importe schreiben am Schacht ohne Herkunftsangabe.
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.Geschrieben, schacht.SetFieldValue("Funktion", "Etwas anderes"));
        Assert.Equal("Etwas anderes", schacht.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Import_der_kanalfirma_ersetzt_einen_katasterwert_der_haltung()
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Kataster, false);

        haltung.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Legacy, false);

        Assert.Equal("Steinzeug", haltung.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Theory]
    [InlineData("Koordinate_East")]
    [InlineData("Koordinate_North")]
    public void Eine_vermessene_katasterlage_ersetzt_kein_import(string feld)
    {
        // Ausnahme zur Kanalfirma-Regel: Die Katasterkoordinate ist vermessen, die des Protokolls meist
        // Handy-GPS. Ein Import fuellt eine leere Lage, ersetzt aber keine vermessene (Regel seit 19.09.2026).
        var schacht = new SchachtRecord();
        schacht.SetFieldValue(feld, "2687939.868", FieldSource.Kataster, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.KatasterwertGeschuetzt, schacht.SetFieldValue(feld, "2687941.2", FieldSource.Pdf, userEdited: false));
        Assert.Equal(FeldSchreibErgebnis.KatasterwertGeschuetzt, schacht.SetFieldValue(feld, "2687941.2"));
        Assert.Equal("2687939.868", schacht.GetFieldValue(feld));

        // Ein neuerer Katasterstand und eine Handkorrektur ersetzen sie weiterhin.
        Assert.Equal(FeldSchreibErgebnis.Geschrieben, schacht.SetFieldValue(feld, "2687940.0", FieldSource.Kataster, userEdited: false));
        Assert.Equal(FeldSchreibErgebnis.Geschrieben, schacht.SetFieldValue(feld, "2687940.5", FieldSource.Manual, userEdited: true));
        Assert.Equal("2687940.5", schacht.GetFieldValue(feld));
    }

    [Fact]
    public void Eine_leere_lage_fuellt_das_protokoll()
    {
        var schacht = new SchachtRecord();

        Assert.Equal(FeldSchreibErgebnis.Geschrieben, schacht.SetFieldValue("Koordinate_East", "2687941.2", FieldSource.Pdf, userEdited: false));
    }

    [Fact]
    public void Unbekannte_herkunft_ersetzt_keinen_katasterwert()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Kataster, userEdited: false);

        var ergebnis = schacht.SetFieldValue("Funktion", "Etwas anderes", FieldSource.Unknown, userEdited: false);

        Assert.Equal(FeldSchreibErgebnis.KatasterwertGeschuetzt, ergebnis);
        Assert.Equal("Schlammsammler", schacht.GetFieldValue("Funktion"));
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
    public void Haltungsmerge_ersetzt_einen_katasterwert_durch_die_kanalfirma()
    {
        var ziel = new HaltungRecord(); var quelle = new HaltungRecord();
        ziel.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Kataster, false);
        quelle.SetFieldValue(FieldKeys.PipeMaterial, "Kunststoff", FieldSource.Pdf, false);
        var ergebnis = AuswertungPro.Next.Infrastructure.Import.Common.MergeEngine.MergeRecord(ziel, quelle, FieldSource.Pdf);
        Assert.Equal("Kunststoff", ziel.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.Equal(1, ergebnis.Updated);
        Assert.Equal(0, ergebnis.Conflicts);
    }

    [Fact]
    public void Haltungsmerge_laesst_eine_handkorrektur_stehen()
    {
        var ziel = new HaltungRecord(); var quelle = new HaltungRecord();
        ziel.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, true);
        quelle.SetFieldValue(FieldKeys.PipeMaterial, "Kunststoff", FieldSource.Pdf, false);
        AuswertungPro.Next.Infrastructure.Import.Common.MergeEngine.MergeRecord(ziel, quelle, FieldSource.Pdf);
        Assert.Equal("Beton", ziel.GetFieldValue(FieldKeys.PipeMaterial));
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
