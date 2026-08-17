using System.Linq;
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

    // ═══════════════════════════════════════════════
    // Formel-Entschaerfung (Codeaudit 2026-08-17)
    //
    // CsvCell entschaerft Formelanfaenge (=, +, -, @, Tab, CR) zentral, und die
    // Projektregel lautet: kein Exportweg setzt das erneut halb um. Dieser
    // Exporter hatte trotzdem ein eigenes Csv(), das nur Anfuehrungszeichen
    // setzte. Das Leistungsverzeichnis geht nach draussen - an Unternehmer und
    // Gemeinde -, und Positionstext sowie Projektname sind freier Text.
    // ═══════════════════════════════════════════════

    [Fact]
    public void BuildCsv_EntschaerftFormelnImPositionstext()
    {
        var positions = new[]
        {
            new AggregatedPosition("311.111", "300", "A", "=HYPERLINK(\"http://x\";\"klick\")",
                "h", null, 1m, 10m, 1, false, 10m)
        };

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions);

        Assert.DoesNotContain(";=HYPERLINK", csv);
        Assert.Contains("HYPERLINK", csv);
    }

    [Fact]
    public void BuildCsv_ProjektnameKannKeineFormelStarten()
    {
        // Der Projektname wird in "Sanierungsmassnahmen ... Projekt: <name>"
        // eingebettet. Die Zelle beginnt damit nie mit einem Formelzeichen, und
        // ein "=" mitten im Text ist fuer Excel harmlos. Festgehalten, damit
        // niemand den Kopftext spaeter so umbaut, dass der Name vorne steht.
        var positions = new[]
        {
            new AggregatedPosition("311.111", "300", "A", "Roboter", "h", null, 1m, 10m, 1, false, 10m)
        };

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions, projectName: "=CMD|calc");

        var kopfzeile = csv.Split('\n').First(z => z.Contains("Projekt:"));
        Assert.False(kopfzeile.TrimStart('"').StartsWith('='),
            "Die Kopfzeile darf nicht mit einem Formelzeichen beginnen.");
    }

    [Fact]
    public void BuildCsv_LaesstNegativeZahlenZahlenBleiben()
    {
        // Die Entschaerfung darf keine Minuszahl in Text verwandeln - genau
        // deshalb gibt es CsvCell und nicht ein pauschales Apostroph.
        var positions = new[]
        {
            new AggregatedPosition("311.111", "300", "A", "-12.5", "h", null, 1m, 10m, 1, false, 10m)
        };

        var csv = NpkLeistungsverzeichnisExporter.BuildCsv(positions);

        Assert.DoesNotContain("'-12.5", csv);
    }
}
