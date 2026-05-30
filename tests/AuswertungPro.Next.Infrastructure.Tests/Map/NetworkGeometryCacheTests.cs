using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class NetworkGeometryCacheTests
{
    private const string MiniXtf = @"<?xml version='1.0' encoding='UTF-8'?>
<TRANSFER><DATASECTION><X>
  <a.Haltung TID='h1'><Bezeichnung>A-B</Bezeichnung>
    <Verlauf><POLYLINE><COORD><C1>2690000</C1><C2>1190000</C2></COORD>
    <COORD><C1>2690010</C1><C2>1190000</C2></COORD></POLYLINE></Verlauf>
  </a.Haltung></X></DATASECTION></TRANSFER>";

    [Fact]
    public void Load_BautCacheAusXtf_undLiefertHaltungen()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var xtf = Path.Combine(dir, "n.xtf");
        var cache = Path.Combine(dir, "cache.json");
        File.WriteAllText(xtf, MiniXtf);
        try
        {
            var sut = new NetworkGeometryCache(cacheFilePath: cache);
            var first = sut.Load(xtf).ToList();
            Assert.Single(first);
            Assert.True(File.Exists(cache));
            var second = sut.Load(xtf).ToList();
            Assert.Equal("A-B", second[0].Haltungsname);
        }
        finally { Directory.Delete(dir, true); }
    }
}
