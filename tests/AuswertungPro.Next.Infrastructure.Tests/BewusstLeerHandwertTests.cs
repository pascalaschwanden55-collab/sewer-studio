using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Entscheid Pascal 02.10.2026 (E3, Deepscan R10): «Handwert, auch bewusst leer» hat Vorrang.
/// Ein Feld, das der Nutzer von Hand geleert hat (UserEdited=true, Wert leer), fuellt kein
/// Import und kein Abgleich. Die gemeinsame Stelle ist <c>FuelleLeeresFeld</c> an beiden
/// Datensaetzen; ein normal leeres Feld wird weiter gefuellt.
/// </summary>
public sealed class BewusstLeerHandwertTests
{
    [Fact]
    public void Haltung_bewusst_leer_wird_nicht_gefuellt()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.PipeMaterial, "", FieldSource.Manual, userEdited: true);

        Assert.True(h.IstBewusstLeer(FieldKeys.PipeMaterial));
        Assert.False(h.FuelleLeeresFeld(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Kataster));

        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.True(h.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
    }

    [Fact]
    public void Haltung_im_raster_geleert_bleibt_leer()
    {
        // Die Rasterbindung kann direkt in Fields schreiben; die Handmarke bleibt dann stehen.
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);
        h.Fields[FieldKeys.PipeMaterial] = "";

        Assert.False(h.FuelleLeeresFeld(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Kataster));
        Assert.Equal("", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Haltung_normal_leer_wird_gefuellt()
    {
        var h = new HaltungRecord();

        Assert.False(h.IstBewusstLeer(FieldKeys.PipeMaterial));
        Assert.True(h.FuelleLeeresFeld(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Kataster));

        Assert.Equal("Steinzeug", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.False(h.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.PipeMaterial].Source);
    }

    [Fact]
    public void Handwert_mit_inhalt_ist_nicht_bewusst_leer()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);

        Assert.False(h.IstBewusstLeer(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Schacht_bewusst_leer_wird_nicht_gefuellt()
    {
        var s = new SchachtRecord();
        s.SetFieldValue(FieldKeys.ShaftDimension1Mm, "", FieldSource.Manual, userEdited: true);

        Assert.True(s.IstBewusstLeer(FieldKeys.ShaftDimension1Mm));
        Assert.False(s.FuelleLeeresFeld(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Kataster));

        Assert.Equal("", s.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.True(s.IsUserEdited(FieldKeys.ShaftDimension1Mm));
    }

    [Fact]
    public void Schacht_normal_leer_wird_gefuellt()
    {
        var s = new SchachtRecord();

        Assert.False(s.IstBewusstLeer(FieldKeys.ShaftDimension1Mm));
        Assert.True(s.FuelleLeeresFeld(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Kataster));

        Assert.Equal("600", s.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.False(s.IsUserEdited(FieldKeys.ShaftDimension1Mm));
    }
}
