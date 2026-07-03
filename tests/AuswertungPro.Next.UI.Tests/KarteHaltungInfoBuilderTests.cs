using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Infopanel-Daten fuer die Karte (Klick auf eine Haltung).</summary>
public sealed class KarteHaltungInfoBuilderTests
{
    [Fact]
    public void Build_FuelltAlleFelder()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "58951-58950", FieldSource.Xtf, false);
        record.SetFieldValue("DN_mm", "600", FieldSource.Xtf, false);
        record.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, false);
        record.SetFieldValue("Haltungslaenge_m", "30.4", FieldSource.Xtf, false);
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Xtf, false);
        record.SetFieldValue("Link", @"Haltungen_Verteilt\x.mp4", FieldSource.Xtf, false);

        var info = KarteHaltungInfoBuilder.Build(record);

        Assert.NotNull(info);
        Assert.Equal("58951-58950", info!.Name);
        Assert.Equal("DN 600", info.Dn);
        Assert.Equal("Beton", info.Material);
        Assert.Equal("30.4 m", info.Laenge);
        Assert.Equal("2", info.Zustandsklasse);
        Assert.True(info.HatVideo);
    }

    [Fact]
    public void Build_LeereFelderWerdenZuStrich_OhneVideoFalse()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "X", FieldSource.Xtf, false);

        var info = KarteHaltungInfoBuilder.Build(record)!;

        Assert.Equal("—", info.Dn);
        Assert.Equal("—", info.Material);
        Assert.Equal("—", info.Laenge);
        Assert.Equal("—", info.Zustandsklasse);
        Assert.False(info.HatVideo);
    }

    [Fact]
    public void Build_NullRecord_LiefertNull()
    {
        Assert.Null(KarteHaltungInfoBuilder.Build(null));
    }
}
