using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Chip-Filter fuers Haltungen-Grid: Zustandsklasse, mit Video, mit Schaeden.
/// Reiner View-Filter — NR-Laufnummer/Reihenfolge bleiben unangetastet.
/// </summary>
public sealed class DataPageFilterPolicyTests
{
    private static HaltungRecord Haltung(string zk = "", string link = "", string schaeden = "")
    {
        var record = new HaltungRecord();
        if (zk.Length > 0) record.SetFieldValue("Zustandsklasse", zk, FieldSource.Xtf, false);
        if (link.Length > 0) record.SetFieldValue("Link", link, FieldSource.Xtf, false);
        if (schaeden.Length > 0) record.SetFieldValue("Primaere_Schaeden", schaeden, FieldSource.Xtf, false);
        return record;
    }

    [Fact]
    public void InaktiverFilter_LaesstAllesDurch()
    {
        Assert.False(DataPageFilter.Aus.IstAktiv);
        Assert.True(DataPageFilter.Aus.Passt(Haltung()));
    }

    [Fact]
    public void ZustandsklassenFilter_MatchtExakt()
    {
        var filter = new DataPageFilter("2", false, false);

        Assert.True(filter.IstAktiv);
        Assert.True(filter.Passt(Haltung(zk: "2")));
        Assert.False(filter.Passt(Haltung(zk: "3")));
        Assert.False(filter.Passt(Haltung())); // ohne ZK
    }

    [Fact]
    public void VideoUndSchadenFilter_KombinierenMitUnd()
    {
        var filter = new DataPageFilter(null, NurMitVideo: true, NurMitSchaeden: true);

        Assert.True(filter.Passt(Haltung(link: "x.mp4", schaeden: "0.0m BCD")));
        Assert.False(filter.Passt(Haltung(link: "x.mp4")));                  // ohne Schaeden
        Assert.False(filter.Passt(Haltung(schaeden: "0.0m BCD")));           // ohne Video
    }

    [Fact]
    public void NullRecord_PasstNie()
    {
        Assert.False(new DataPageFilter("2", false, false).Passt(null));
    }
}
