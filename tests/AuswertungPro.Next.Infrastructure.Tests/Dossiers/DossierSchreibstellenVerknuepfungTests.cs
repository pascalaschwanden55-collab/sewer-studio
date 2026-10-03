using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Testnetz T5 (Deepscan 02.10.2026): Jede Dossier-Schreibstelle lehnt ein Verknüpfungsziel ab,
/// ohne etwas in den fremden Ordner zu schreiben. Die Ablage heisst überall <c>Dossiers\Haus</c>,
/// wobei <c>Haus</c> (oder <c>Dossiers</c> selbst) eine Verknüpfung auf einen Fremdordner ist.
/// </summary>
public sealed class DossierSchreibstellenVerknuepfungTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "sewerstudio-dossier-link-" + Guid.NewGuid().ToString("N"));
    private readonly string _projekt;
    private readonly string _fremd;
    private readonly string _dossiers;
    private readonly string _haus;

    public DossierSchreibstellenVerknuepfungTests()
    {
        _projekt = Path.Combine(_root, "Projekt");
        _fremd = Path.Combine(_root, "Fremd");
        _dossiers = Path.Combine(_projekt, DossierFolderPlanner.DossierRootFolderName);
        _haus = Path.Combine(_dossiers, "Musterweg-7");
        Directory.CreateDirectory(_projekt);
        Directory.CreateDirectory(_fremd);
    }

    public void Dispose()
    {
        // Verknüpfungen zuerst lösen, damit der rekursive Löschlauf nie ins Fremdziel greift.
        foreach (var link in new[] { _haus, _dossiers })
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

    private void HausAlsVerknuepfung()
    {
        Directory.CreateDirectory(_dossiers);
        JunctionTestSupport.CreateDirectoryLink(_haus, _fremd);
    }

    private static void Abgelehnt(Action aktion)
    {
        var fehler = Assert.ThrowsAny<Exception>(aktion);
        Assert.Contains("Verknüpfung", fehler.Message, StringComparison.OrdinalIgnoreCase);
    }

    private string Quelle()
    {
        var pfad = Path.Combine(_root, "quelle.pdf");
        File.WriteAllBytes(pfad, [1, 2, 3]);
        return pfad;
    }

    [JunctionFact]
    public void Beilagen_Kopie_in_verknuepften_Dossierordner_wird_ohne_Schreiben_abgelehnt()
    {
        HausAlsVerknuepfung();

        Abgelehnt(() => DossierAttachmentFilePublisher.CopyAtomically(
            Quelle(), Path.Combine(_haus, "plan.pdf"), new ProjectWritePathGuard(_projekt)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public void Beilagen_Bytes_in_verknuepften_Dossierordner_werden_ohne_Schreiben_abgelehnt()
    {
        HausAlsVerknuepfung();

        Abgelehnt(() => DossierAttachmentFilePublisher.WriteAllBytesAtomically(
            [1, 2, 3], Path.Combine(_haus, "plan.pdf"), new ProjectWritePathGuard(_projekt)));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public void Eigentuemermanifest_Load_und_Commit_auf_verknuepftem_Ordner_werden_abgelehnt()
    {
        HausAlsVerknuepfung();
        var guard = new ProjectWritePathGuard(_projekt);
        var warnungen = new List<string>();

        Abgelehnt(() => DossierAttachmentOwnershipManifest.Load(_haus, guard, warnungen));
        Abgelehnt(() => DossierAttachmentOwnershipManifest.Commit(
            _haus,
            guard,
            DossierAttachmentOwnershipSnapshot.Empty,
            [],
            hasUnresolvedSelections: false,
            new DossierAttachmentPublishSession(warnungen),
            warnungen,
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public async Task Dossierdatei_Speichern_mit_verknuepftem_Dossierwurzelordner_schreibt_nichts_nach_aussen()
    {
        JunctionTestSupport.CreateDirectoryLink(_dossiers, _fremd);
        var store = new DossierFileStore();
        var dokument = new DossierDocument { Area = new DossierAreaSettings { AreaTitle = "Gebiet" } };

        var fehler = await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(_projekt, dokument));
        Assert.Contains("Verknüpfung", fehler.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public async Task Word_Export_in_verknuepften_Dossierordner_schreibt_nichts_nach_aussen()
    {
        HausAlsVerknuepfung();
        var vorlage = Path.Combine(_root, "vorlage.docx");
        File.WriteAllBytes(vorlage, [0]);

        var ergebnis = await new DossierWordTemplateExportService(() => vorlage)
            .ExportAsync(Anfrage(_haus));

        Assert.False(ergebnis.Success);
        Assert.Contains("Verknüpfung", ergebnis.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    [JunctionFact]
    public async Task Bauteilliste_in_verknuepften_Dossierordner_schreibt_nichts_nach_aussen()
    {
        HausAlsVerknuepfung();
        var dienst = new DossierComponentListExportService(
            new FesteListe(), new FesteSchachtListe(), () => new DateTime(2026, 10, 3));

        var haltungen = await dienst.CreateHoldingListAsync(Anfrage(_haus));
        var schaechte = await dienst.CreateShaftListAsync(Anfrage(_haus));

        Assert.False(haltungen.Success);
        Assert.False(schaechte.Success);
        Assert.Contains("Verknüpfung", haltungen.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Verknüpfung", schaechte.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_fremd));
    }

    private DossierExportRequest Anfrage(string zielOrdner)
    {
        var projekt = new Project();
        var dossier = new DossierDefinition
        {
            Name = "Musterweg 7",
            FolderName = "Musterweg-7",
            OwnerName = "Muster Eigentümer"
        };
        var snapshot = DossierSnapshotBuilder.Build(dossier, projekt, null, null);
        return new DossierExportRequest(projekt, _projekt, new DossierAreaSettings(), dossier, snapshot, zielOrdner);
    }

    private sealed class FesteListe : IDossierHoldingListPdfService
    {
        public byte[] CreatePdf(DossierHoldingListPdfModel model) => [1, 2, 3];
    }

    private sealed class FesteSchachtListe : IDossierShaftListPdfService
    {
        public byte[] CreatePdf(DossierShaftListPdfModel model) => [4, 5, 6];
    }
}
