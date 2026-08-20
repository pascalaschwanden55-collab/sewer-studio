using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Beim manuellen "Schacht verteilen" traegt die Datei das Datum aus dem Protokoll-PDF.
/// Der Import muss denselben Wert verwenden, sonst heisst dieselbe Datei je nach Weg
/// anders ("20231010_80783.pdf" gegen "00000000_80783.pdf").
/// </summary>
public sealed class NameBasedProtocolDistributorSchachtDatumTests : IDisposable
{
    private readonly string _projektOrdner = NeuerOrdner();
    private readonly string _quelle = NeuerOrdner();

    private static string NeuerOrdner()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nbpd_sdat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Steht fuer den echten Leser der Verteilung (PDF-Seiten + Schachtparser).</summary>
    private sealed class FesterDatumsleser : IProtocolPdfDateReader
    {
        private readonly DateTime? _datum;
        public FesterDatumsleser(DateTime? datum) => _datum = datum;
        public DateTime? ReadSchachtDate(string pdfPath) => _datum;
    }

    private string[] VerteilteSchachtdateien()
    {
        var ordner = Path.Combine(_projektOrdner, "Schächte_Verteilt");
        return Directory.Exists(ordner)
            ? Directory.EnumerateFiles(ordner, "*.pdf", SearchOption.AllDirectories)
                .Select(pfad => Path.GetFileName(pfad)).OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
    }

    private Project ProjektMitSchacht(string? ausfuehrungsdatum = null)
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80783");
        if (ausfuehrungsdatum is not null)
            schacht.SetFieldValue("Ausführung Datum/Jahr", ausfuehrungsdatum);
        project.SchaechteData.Add(schacht);
        return project;
    }

    [Fact]
    public void DatumAusDemProtokollPdf_BestimmtDenDateinamen()
    {
        File.WriteAllText(Path.Combine(_quelle, "80783.pdf"), "x");

        new NameBasedProtocolDistributor(
                new ImportPdfReferenceResolver(),
                new FesterDatumsleser(new DateTime(2023, 10, 10)))
            .Distribute(ProjektMitSchacht(), _projektOrdner, _quelle);

        Assert.Equal(new[] { "20231010_80783.pdf" }, VerteilteSchachtdateien());
    }

    [Fact]
    public void ProtokollDatum_SchlaegtDasFeldAmSchacht()
    {
        // Die Verteilung liest ausschliesslich das PDF - der Import muss gleich entscheiden.
        File.WriteAllText(Path.Combine(_quelle, "80783.pdf"), "x");

        new NameBasedProtocolDistributor(
                new ImportPdfReferenceResolver(),
                new FesterDatumsleser(new DateTime(2023, 10, 10)))
            .Distribute(ProjektMitSchacht("01.01.2020"), _projektOrdner, _quelle);

        Assert.Equal(new[] { "20231010_80783.pdf" }, VerteilteSchachtdateien());
    }

    [Fact]
    public void OhneDatumImPdf_GiltDasAusfuehrungsdatumAmSchacht()
    {
        File.WriteAllText(Path.Combine(_quelle, "80783.pdf"), "x");

        new NameBasedProtocolDistributor(
                new ImportPdfReferenceResolver(),
                new FesterDatumsleser(null))
            .Distribute(ProjektMitSchacht("28.10.2024"), _projektOrdner, _quelle);

        Assert.Equal(new[] { "20241028_80783.pdf" }, VerteilteSchachtdateien());
    }

    public void Dispose()
    {
        try { Directory.Delete(_projektOrdner, true); } catch { }
        try { Directory.Delete(_quelle, true); } catch { }
    }
}
