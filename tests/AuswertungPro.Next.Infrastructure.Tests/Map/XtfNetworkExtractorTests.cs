using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class XtfNetworkExtractorTests
{
    private const string MiniXtf = @"<?xml version='1.0' encoding='UTF-8'?>
<TRANSFER><DATASECTION><SIA405_ABWASSER_2020_LV95 BID='b'>
  <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID='h1'>
    <Bezeichnung>1039170-85450</Bezeichnung>
    <Verlauf><POLYLINE>
      <COORD><C1>2690511.225</C1><C2>1194863.079</C2></COORD>
      <COORD><C1>2690500.961</C1><C2>1194862.355</C2></COORD>
    </POLYLINE></Verlauf>
  </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
</SIA405_ABWASSER_2020_LV95></DATASECTION></TRANSFER>";

    [Fact]
    public void Extract_LiestHaltungMitPolylinie()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, MiniXtf);
        try
        {
            var result = new XtfNetworkExtractor().Extract(path).ToList();
            Assert.Single(result);
            Assert.Equal("1039170-85450", result[0].Haltungsname);
            Assert.Equal(2, result[0].Points.Count);
            Assert.Equal(2690511.225, result[0].Points[0].X, 3);
            Assert.Equal(1194863.079, result[0].Points[0].Y, 3);
        }
        finally { File.Delete(path); }
    }
}
