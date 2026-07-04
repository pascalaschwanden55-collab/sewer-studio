using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class NpkLeistungsverzeichnisExporterTests
{
    [Fact]
    public void BuildCsv_Warnt_Wenn_Gleiche_NpkNummer_Unterschiedliche_Einheiten_Hat()
    {
        var positions = new[]
        {
            new AggregatedPosition("311.111", "300", "A", "Roboter", "h", null, 2m, 580m, 1, false, 290m),
            new AggregatedPosition("311.111", "300", "B", "Fraesen", "m", null, 10m, 290m, 1, false, 29m)
        };

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions, "CHF");

        Assert.Contains("WARNUNG: NPK 311.111 kommt mit unterschiedlichen Einheiten vor: h, m", csv);
    }

    [Fact]
    public void BuildCsv_Schreibt_NichtEnthaltene_Pauschalkosten_Als_Fussnote()
    {
        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(
            Array.Empty<AggregatedPosition>(),
            "CHF",
            excludedPauschaleTotal: 50m,
            excludedPauschaleHoldingCount: 1);

        Assert.Contains("Nicht enthaltene Pauschalkosten (1 Haltung(en));;;;;50.00;", csv);
    }
}
