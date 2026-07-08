using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class BackupVersionRetentionTests
{
    [Fact]
    public void BuildStandName_IstSortierbarUndWirdErkannt()
    {
        var frueh = BackupVersionRetention.BuildStandName(new DateTime(2026, 7, 8, 9, 5, 3));
        var spaet = BackupVersionRetention.BuildStandName(new DateTime(2026, 7, 8, 10, 0, 0));

        Assert.Equal("2026-07-08_090503", frueh);
        Assert.True(string.CompareOrdinal(frueh, spaet) < 0);
        Assert.True(BackupVersionRetention.IsStandName(frueh));
        Assert.True(BackupVersionRetention.IsStandName(spaet));
    }

    [Theory]
    [InlineData("kein-stand")]
    [InlineData("2026-07-08")]
    [InlineData("2026-07-08_0905")]
    [InlineData("2026-13-40_990000")]
    [InlineData("")]
    public void IsStandName_LehntFremdeNamenAb(string name)
        => Assert.False(BackupVersionRetention.IsStandName(name));

    [Fact]
    public void IsVersionsDir_ErkenntNurDenVersionsOrdner()
    {
        Assert.True(BackupVersionRetention.IsVersionsDir("_Versionen"));
        Assert.True(BackupVersionRetention.IsVersionsDir(Path.Combine("_Versionen", "2026-07-08_090503", "Programm")));
        Assert.False(BackupVersionRetention.IsVersionsDir("Programm"));
        Assert.False(BackupVersionRetention.IsVersionsDir(Path.Combine("Programm", "_Versionen")));
    }

    [Fact]
    public void BuildVersionsRelativePath_LiegtImStandOrdner()
    {
        var rel = BackupVersionRetention.BuildVersionsRelativePath(
            "2026-07-08_090503",
            Path.Combine("Programm", "src", "app.cs"));

        Assert.Equal(
            Path.Combine("_Versionen", "2026-07-08_090503", "Programm", "src", "app.cs"),
            rel);
    }

    [Fact]
    public void SelectStaendeToDelete_WaehltNurDieAeltestenUeberDemLimit()
    {
        var staende = Enumerable.Range(0, 13)
            .Select(i => BackupVersionRetention.BuildStandName(new DateTime(2026, 1, 1).AddHours(i)))
            .ToArray();

        var loeschen = BackupVersionRetention.SelectStaendeToDelete(staende, maxKeep: 10);

        Assert.Equal(3, loeschen.Count);
        Assert.Contains(staende[0], loeschen);
        Assert.Contains(staende[1], loeschen);
        Assert.Contains(staende[2], loeschen);
        Assert.DoesNotContain(staende[3], loeschen);
    }

    [Fact]
    public void SelectStaendeToDelete_IgnoriertFremdeNamenUndKleineListen()
    {
        var namen = new[] { "kein-stand", "2026-07-08_090503", "notizen" };

        var loeschen = BackupVersionRetention.SelectStaendeToDelete(namen, maxKeep: 1);

        Assert.Empty(loeschen);
    }
}
