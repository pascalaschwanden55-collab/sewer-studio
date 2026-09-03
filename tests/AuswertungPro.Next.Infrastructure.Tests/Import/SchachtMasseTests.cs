using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die Schachtmasse leben in genau zwei Zahlenfeldern: "Dimension 1 mm" und
/// "Dimension 2 mm". Rund heisst beide gleich (600 / 600), oval oder eckig heisst
/// zwei verschiedene (1100 / 900). Das alte Textfeld "Dimension" ("600 mm",
/// "1100 x 900 mm") wird beim Laden in die zwei Felder uebernommen und verschwindet.
///
/// Anlass 2026-09-03: Jeder Import schrieb den Text, nur die Handeingabe und das
/// Nachfuellen aus QGIS die zwei Zahlen. Im Bestand trugen 61 Schaechte den Text und
/// 2 die Zahlen; Export und Anzeige zeigten dadurch verschiedene Werte.
/// </summary>
public sealed class SchachtMasseTests
{
    [Theory]
    [InlineData("600 mm", "600", "600")]
    [InlineData("1100 x 900 mm", "1100", "900")]
    [InlineData("800", "800", "800")]
    [InlineData("1200 / 800", "1200", "800")]
    public void Ein_Text_wird_in_zwei_Masse_gelesen(string text, string eins, string zwei)
    {
        var masse = SchachtMasse.Lies(text);

        Assert.NotNull(masse);
        Assert.Equal(eins, masse.Value.Dimension1);
        Assert.Equal(zwei, masse.Value.Dimension2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unbekannt")]
    [InlineData("0 mm")]
    [InlineData("a x b")]
    public void Unbrauchbarer_Text_liefert_nichts(string text)
        => Assert.Null(SchachtMasse.Lies(text));

    [Theory]
    [InlineData("1100", "900", "1100", "900")]
    [InlineData("600", null, "600", "600")]
    [InlineData(null, "600", "600", "600")]
    [InlineData("600", "", "600", "600")]
    public void Zwei_Rohwerte_werden_zu_einem_Paar(string? a, string? b, string eins, string zwei)
    {
        var masse = SchachtMasse.AusZwei(a, b);

        Assert.NotNull(masse);
        Assert.Equal(eins, masse.Value.Dimension1);
        Assert.Equal(zwei, masse.Value.Dimension2);
    }

    [Fact]
    public void Ohne_Rohwerte_gibt_es_kein_Paar()
        => Assert.Null(SchachtMasse.AusZwei(null, ""));

    [Fact]
    public void Schreiben_setzt_beide_Felder_mit_Herkunft()
    {
        var record = new SchachtRecord();

        var geschrieben = SchachtMasse.Schreibe(record, ("1100", "900"), FieldSource.Pdf, userEdited: false);

        Assert.True(geschrieben);
        Assert.Equal("1100", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("900", record.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal(FieldSource.Pdf, record.FieldMeta[FieldKeys.ShaftDimension1Mm].Source);
        Assert.False(record.FieldMeta[FieldKeys.ShaftDimension2Mm].UserEdited);
    }

    [Fact]
    public void Schreiben_ueberschreibt_keine_Handeingabe()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1000", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "1000", FieldSource.Manual, userEdited: true);

        var geschrieben = SchachtMasse.Schreibe(record, ("600", "600"), FieldSource.Xtf405, userEdited: false);

        Assert.False(geschrieben);
        Assert.Equal("1000", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
    }

    [Fact]
    public void Nur_leere_Felder_werden_gefuellt_wenn_verlangt()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "800", FieldSource.Xtf405, userEdited: false);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "800", FieldSource.Xtf405, userEdited: false);

        var geschrieben = SchachtMasse.Schreibe(record, ("600", "600"), FieldSource.Pdf, userEdited: false, nurLeere: true);

        Assert.False(geschrieben);
        Assert.Equal("800", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
    }

    [Fact]
    public void Das_alte_Textfeld_wandert_in_die_zwei_Felder_und_verschwindet()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Dimension", "1100 x 900 mm", FieldSource.Pdf, userEdited: false);

        var geaendert = SchachtMasse.UebernimmAlteTextfelder(record);

        Assert.True(geaendert);
        Assert.Equal("1100", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("900", record.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal(FieldSource.Pdf, record.FieldMeta[FieldKeys.ShaftDimension1Mm].Source);
        Assert.False(record.Fields.ContainsKey("Dimension"));
        Assert.False(record.FieldMeta.ContainsKey("Dimension"));
    }

    [Fact]
    public void Eine_Handeingabe_im_Textfeld_bleibt_eine_Handeingabe()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Dimension", "600 mm", FieldSource.Manual, userEdited: true);

        SchachtMasse.UebernimmAlteTextfelder(record);

        Assert.True(record.FieldMeta[FieldKeys.ShaftDimension1Mm].UserEdited);
        Assert.Equal("600", record.GetFieldValue(FieldKeys.ShaftDimension2Mm));
    }

    [Fact]
    public void Gefuellte_Zahlenfelder_gewinnen_gegen_den_Text()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Dimension", "600 mm", FieldSource.Pdf, userEdited: false);
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, userEdited: true);

        var geaendert = SchachtMasse.UebernimmAlteTextfelder(record);

        Assert.True(geaendert);
        Assert.Equal("1100", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.False(record.Fields.ContainsKey("Dimension"));
    }

    [Fact]
    public void Auch_das_leere_Textfeld_und_Durchmesser_verschwinden()
    {
        var record = new SchachtRecord();
        record.Fields["Dimension"] = "";
        record.SetFieldValue("Durchmesser", "1000 x 800", FieldSource.Legacy, userEdited: false);

        var geaendert = SchachtMasse.UebernimmAlteTextfelder(record);

        Assert.True(geaendert);
        Assert.Equal("1000", record.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("800", record.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.False(record.Fields.ContainsKey("Dimension"));
        Assert.False(record.Fields.ContainsKey("Durchmesser"));
    }

    [Fact]
    public void Ein_unlesbarer_Text_bleibt_stehen_statt_zu_verschwinden()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Dimension", "siehe Plan", FieldSource.Pdf, userEdited: false);

        var geaendert = SchachtMasse.UebernimmAlteTextfelder(record);

        Assert.False(geaendert);
        Assert.Equal("siehe Plan", record.GetFieldValue("Dimension"));
    }

    [Fact]
    public void Ohne_alte_Felder_passiert_nichts()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Manual, userEdited: true);

        Assert.False(SchachtMasse.UebernimmAlteTextfelder(record));
    }

    [Fact]
    public void Der_Projektlauf_zaehlt_die_geaenderten_Schaechte()
    {
        var a = new SchachtRecord();
        a.SetFieldValue("Dimension", "600 mm", FieldSource.Pdf, userEdited: false);
        var b = new SchachtRecord();

        Assert.Equal(1, SchachtMasse.UebernimmAlteTextfelder([a, b]));
    }
}
