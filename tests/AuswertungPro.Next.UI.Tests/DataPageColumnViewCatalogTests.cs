using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 1: feste Spaltenansichten der Haltungsliste.</summary>
public sealed class DataPageColumnViewCatalogTests
{
    [Fact]
    public void Kompakt_zeigt_Name_Strasse_Material_DN_Laenge_Zustand_und_Video()
    {
        var v = DataPageColumnViewCatalog.Resolve("kompakt");
        Assert.Equal(
            new[] { FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass, FieldKeys.Link },
            v.Felder);
    }

    [Fact]
    public void Alle_hat_keine_Feldliste_und_ist_der_Rueckfall()
    {
        Assert.Null(DataPageColumnViewCatalog.Resolve("alle").Felder);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve(null).Key);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve("gibt-es-nicht").Key);
        Assert.Equal("kompakt", DataPageColumnViewCatalog.Resolve("KOMPAKT").Key);
    }

    [Fact]
    public void Jedes_Feld_einer_Ansicht_existiert_im_Feldkatalog()
    {
        var bekannt = new HashSet<string>(FieldCatalog.ColumnOrder, StringComparer.Ordinal);
        foreach (var v in DataPageColumnViewCatalog.Views)
            foreach (var f in v.Felder ?? Array.Empty<string>())
                Assert.True(bekannt.Contains(f), $"{v.Key}: {f} fehlt im FieldCatalog");
    }

    [Fact]
    public void Der_Haltungsname_steht_in_jeder_Ansicht_vorn_und_Schluessel_sind_eindeutig()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in DataPageColumnViewCatalog.Views)
        {
            Assert.True(keys.Add(v.Key), $"Schluessel {v.Key} doppelt");
            if (v.Felder is not null)
                Assert.Equal(FieldKeys.HoldingName, v.Felder[0]);
        }
        Assert.Equal(6, DataPageColumnViewCatalog.Views.Count);
    }
}
