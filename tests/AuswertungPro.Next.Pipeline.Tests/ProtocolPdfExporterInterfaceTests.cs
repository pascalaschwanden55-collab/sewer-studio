using System.Text;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfExporterInterfaceTests
{
    [Fact]
    public void Konkreter_Exporter_erfuellt_den_stabilen_Vertrag()
    {
        IProtocolPdfExporter exporter = new ProtocolPdfExporter();
        var document = new ProtocolDocument { HaltungId = "12-34" };
        document.Current.Entries.Add(new ProtocolEntry { Code = "BAJ" });

        var csv = Encoding.UTF8.GetString(exporter.BuildCsv(document));

        Assert.Contains("HaltungId", csv);
        Assert.Contains("12-34", csv);
        Assert.Contains("BAJ", csv);
    }
}
