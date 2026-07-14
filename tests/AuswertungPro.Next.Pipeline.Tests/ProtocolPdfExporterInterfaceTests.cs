using System.Text;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;

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

    [Fact]
    public void Konkreter_Exporter_liegt_in_der_Infrastructure_Schicht()
    {
        Assert.Equal(
            typeof(ProtocolRegenerationAdapter).Assembly,
            typeof(ProtocolPdfExporter).Assembly);
        Assert.NotEqual(
            typeof(IProtocolPdfExporter).Assembly,
            typeof(ProtocolPdfExporter).Assembly);
    }

    [Fact]
    public void Application_Schicht_hat_keine_QuestPdf_Abhaengigkeit()
    {
        var applicationRoot = TestRepoPaths.RepoFile("src", "AuswertungPro.Next.Application");
        var projectFile = File.ReadAllText(Path.Combine(
            applicationRoot,
            "AuswertungPro.Next.Application.csproj"));
        var offenders = Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("using QuestPDF", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(applicationRoot, path))
            .ToArray();

        Assert.DoesNotContain("QuestPDF", projectFile, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(offenders);
    }
}
