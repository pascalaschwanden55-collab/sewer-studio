using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.SchachtPro;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Testnetz T5 (Deepscan 02.10.2026): Verteilbericht, SchachtPro-QR-Ablage und Projektdateiprüfer
/// betreten kein Verknüpfungsziel und schreiben nichts in den fremden Ordner.
/// </summary>
public sealed class ImportSchreibstellenVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sewerstudio-import-link-" + Guid.NewGuid().ToString("N"));
    private readonly string _projekt;
    private readonly string _fremd;
    private readonly List<string> _links = [];

    public ImportSchreibstellenVerknuepfungTests()
    {
        _projekt = Path.Combine(_root, "Projekt");
        _fremd = Path.Combine(_root, "Fremd");
        Directory.CreateDirectory(_projekt);
        Directory.CreateDirectory(_fremd);
    }

    public void Dispose()
    {
        foreach (var link in _links)
        {
            try
            {
                if (Directory.Exists(link) && new DirectoryInfo(link).LinkTarget is not null)
                    Directory.Delete(link);
            }
            catch
            {
                // Nur Test-Aufräumen.
            }
        }

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Nur Test-Aufräumen.
        }
    }

    private void Verknuepfe(string link)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        JunctionTestSupport.CreateDirectoryLink(link, _fremd);
        _links.Add(link);
    }

    [JunctionFact]
    public void Verteilbericht_in_verknuepftem_Berichtsordner_wird_nicht_geschrieben()
    {
        Verknuepfe(Path.Combine(_projekt, ProjectStructure.ImportReports));

        var pfad = new VerteilberichtAblage().Schreibe(_projekt, "Haltungen", "Text");

        Assert.Null(pfad);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public void SchachtPro_QR_Ablage_in_verknuepftem_Schachtordner_wird_ohne_Kopie_abgelehnt()
    {
        Directory.CreateDirectory(Path.Combine(_projekt, "Projektdateien"));
        var projektdatei = Path.Combine(_projekt, "Projektdateien", "projekt.json");
        File.WriteAllText(projektdatei, "{}");
        var quelle = Path.Combine(_root, "qr.png");
        File.WriteAllBytes(quelle, [1, 2, 3]);
        Verknuepfe(ProjectStructure.SchachtVerteiltDir(_projekt, "S-1"));
        using var session = new ImportFileStagingService().Begin(projektdatei)
            ?? throw new InvalidOperationException("Staging-Sitzung fehlt.");

        var fehler = Assert.ThrowsAny<Exception>(() => SchachtProQrAblage.Prepare(
            quelle, new ProtocolDto { SchachtNr = "S-1", Datum = "03.10.2026" }, session, CancellationToken.None));
        Assert.Contains("Verknüpfung", fehler.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public void Projektdateipruefer_liest_keine_Datei_ueber_eine_Verknuepfung()
    {
        File.WriteAllBytes(Path.Combine(_fremd, "a.mp4"), [1, 2, 3]);
        Verknuepfe(Path.Combine(_projekt, "Medien"));

        var lesbar = ImportProjektdateiPruefer.IstLesbar(Path.Combine("Medien", "a.mp4"), _projekt, null, out var grund);

        Assert.False(lesbar);
        Assert.Contains("Verknüpfung", grund, StringComparison.OrdinalIgnoreCase);
    }
}
