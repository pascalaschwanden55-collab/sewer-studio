using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierAttachmentCollectorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "dossier_beilagen_" + Guid.NewGuid().ToString("N"));

    private readonly string _projectRoot;
    private readonly string _dossierFolder;

    public DossierAttachmentCollectorTests()
    {
        _projectRoot = Path.Combine(_root, "Projekt");
        _dossierFolder = Path.Combine(_projectRoot, "Dossiers", "Braemenhofstatt");
        Directory.CreateDirectory(_dossierFolder);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Nimmt_das_Original_PDF_des_Kanalunternehmers()
    {
        var original = Path.Combine(_root, "36080-36086_Fretz.pdf");
        await File.WriteAllTextAsync(original, "Original");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>> { ["36080-36086"] = new() { original } });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.OriginalProtocol, attachment.Kind);
        Assert.True(File.Exists(attachment.SourcePath));
        Assert.Equal("Original", await File.ReadAllTextAsync(attachment.SourcePath));
        Assert.StartsWith("01_TV_", attachment.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aktualisiert_eine_verifizierte_automatische_Kopie_atomar()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Version 1");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var first = Assert.Single((await collector.CollectAsync(request)).Attachments);
        await File.WriteAllTextAsync(original, "Version 2");

        var second = Assert.Single((await collector.CollectAsync(request)).Attachments);

        Assert.Equal(first.SourcePath, second.SourcePath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Version 2", await File.ReadAllTextAsync(second.SourcePath));
    }

    [Fact]
    public async Task Manifestfehler_stellt_die_vorherige_automatische_Kopie_wieder_her()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Version 1");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var first = Assert.Single((await collector.CollectAsync(request)).Attachments);
        var manifest = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.json"));
        await File.WriteAllTextAsync(original, "Version 2");

        using (new FileStream(
                   manifest,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => collector.CollectAsync(request));
        }

        Assert.Equal("Version 1", await File.ReadAllTextAsync(first.SourcePath));
        Assert.Empty(Directory.EnumerateFiles(AttachmentFolder(), "*.rollback"));
    }

    [Fact]
    public async Task Faellt_auf_das_eigene_Protokoll_zurueck_wenn_kein_Original_da_ist()
    {
        var (request, record) = BuildScenario(originals: new());
        record.Protocol = new ProtocolDocument();

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.GeneratedProtocol, attachment.Kind);
        Assert.True(File.Exists(attachment.SourcePath));
        Assert.StartsWith("01_Protokoll_", attachment.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_Original_und_ohne_Protokoll_wird_die_Luecke_gemeldet()
    {
        var (request, record) = BuildScenario(originals: new());
        record.Protocol = null;

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        var attachment = Assert.Single(result.Attachments);
        Assert.Equal(DossierAttachmentKind.Missing, attachment.Kind);
        Assert.Equal(1, result.MissingCount);
        Assert.Contains(result.Warnings, w => w.Contains("fehlt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Von_Hand_hinzugelegte_Beilagen_bleiben_erhalten()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Original");

        var eigene = Path.Combine(
            _dossierFolder, DossierFolderPlanner.AttachmentFolderName, "00_QGIS_Plan.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(eigene)!);
        await File.WriteAllTextAsync(eigene, "Mein Plan");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>> { ["36080-36086"] = new() { original } });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        await collector.CollectAsync(request);

        Assert.True(File.Exists(eigene));
        Assert.Equal("Mein Plan", await File.ReadAllTextAsync(eigene));
    }

    [Fact]
    public async Task Kopierfehler_laesst_die_bisherige_automatische_Datei_unveraendert()
    {
        var source = Path.Combine(_root, "gesperrtes_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Neuer Inhalt");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        await File.WriteAllTextAsync(target, "Bisheriger Inhalt");
        using var sourceLock = new FileStream(
            source,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var guard = new ProjectWritePathGuard(_projectRoot);

        Assert.ThrowsAny<IOException>(() =>
            DossierAttachmentFilePublisher.CopyAtomically(source, target, guard));

        Assert.Equal("Bisheriger Inhalt", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(AttachmentFolder(), "*.tmp"));
    }

    [Fact]
    public async Task Zwischenzeitlich_veraenderte_Zieldatei_wird_nicht_ueberschrieben()
    {
        var source = Path.Combine(_root, "neues_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Neue automatische Kopie");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        await File.WriteAllTextAsync(target, "Verifizierter Stand");
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(target)));
        await File.WriteAllTextAsync(target, "Inzwischen manuell geaendert");
        var guard = new ProjectWritePathGuard(_projectRoot);

        Assert.Throws<IOException>(() =>
            DossierAttachmentFilePublisher.CopyAtomically(
                source,
                target,
                guard,
                expectedHash));

        Assert.Equal("Inzwischen manuell geaendert", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.EnumerateFiles(AttachmentFolder(), "*.tmp"));
    }

    [Fact]
    public async Task Manuelle_Datei_nach_dem_Wegstellen_wird_nicht_ueberschrieben()
    {
        var source = Path.Combine(_root, "neues_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Neue automatische Kopie");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        await File.WriteAllTextAsync(target, "Verifizierter Stand");
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(target)));
        var guard = new ProjectWritePathGuard(_projectRoot);

        Assert.ThrowsAny<IOException>(() =>
            DossierAttachmentFilePublisher.CopyAtomically(
                source,
                target,
                guard,
                expectedHash,
                afterExistingTargetStaged: () =>
                    File.WriteAllText(target, "Neue manuelle Datei")));

        Assert.Equal("Neue manuelle Datei", await File.ReadAllTextAsync(target));
        var backup = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.rollback"));
        Assert.Equal("Verifizierter Stand", await File.ReadAllTextAsync(backup));
    }

    [Fact]
    public async Task Ausgetauschte_Publikation_erhaelt_keinen_Manifest_Eintrag()
    {
        var source = Path.Combine(_root, "neues_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Automatische Kopie");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        var warnings = new List<string>();
        var publications = new DossierAttachmentPublishSession(warnings);
        var guard = new ProjectWritePathGuard(_projectRoot);
        DossierAttachmentFilePublisher.CopyAtomically(
            source,
            target,
            guard,
            session: publications);
        await File.WriteAllTextAsync(target, "Manueller Austausch");

        Assert.Throws<IOException>(() =>
            DossierAttachmentOwnershipManifest.Commit(
                AttachmentFolder(),
                guard,
                DossierAttachmentOwnershipSnapshot.Empty,
                [new DossierAttachment(
                    Path.GetFileName(target),
                    target,
                    DossierAttachmentKind.OriginalProtocol,
                    "36080-36086")],
                hasUnresolvedSelections: false,
                publications,
                warnings,
                CancellationToken.None));

        publications.Rollback();
        Assert.Equal("Manueller Austausch", await File.ReadAllTextAsync(target));
        Assert.False(File.Exists(Path.Combine(
            AttachmentFolder(),
            DossierAttachmentOwnershipManifest.FileName)));
    }

    [Fact]
    public async Task Veraenderte_Rollback_Sicherung_wird_beim_Abschluss_nicht_geloescht()
    {
        var source = Path.Combine(_root, "neues_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Neue automatische Kopie");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        await File.WriteAllTextAsync(target, "Bisherige automatische Kopie");
        var previousHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(target)));
        var warnings = new List<string>();
        var publications = new DossierAttachmentPublishSession(warnings);
        var guard = new ProjectWritePathGuard(_projectRoot);
        DossierAttachmentFilePublisher.CopyAtomically(
            source,
            target,
            guard,
            previousHash,
            publications);
        var backup = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.rollback"));
        await File.WriteAllTextAsync(backup, "Fremder Sicherungsinhalt");

        publications.Complete();

        Assert.Equal("Fremder Sicherungsinhalt", await File.ReadAllTextAsync(backup));
        Assert.Contains(warnings, warning => warning.Contains("erhalten", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manueller_Zielinhalt_und_letzte_Auto_Sicherung_bleiben_beim_Rollback()
    {
        var source = Path.Combine(_root, "neues_protokoll.pdf");
        await File.WriteAllTextAsync(source, "Neue automatische Kopie");
        var target = Path.Combine(AttachmentFolder(), "01_TV_36080-36086.pdf");
        await File.WriteAllTextAsync(target, "Letzte gepruefte Kopie");
        var previousHash = Convert.ToHexString(
            SHA256.HashData(await File.ReadAllBytesAsync(target)));
        var warnings = new List<string>();
        var publications = new DossierAttachmentPublishSession(warnings);
        var guard = new ProjectWritePathGuard(_projectRoot);
        DossierAttachmentFilePublisher.CopyAtomically(
            source,
            target,
            guard,
            previousHash,
            publications);
        var backup = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.rollback"));
        await File.WriteAllTextAsync(target, "Manueller Zielinhalt");

        publications.Rollback();

        Assert.Equal("Manueller Zielinhalt", await File.ReadAllTextAsync(target));
        Assert.Equal("Letzte gepruefte Kopie", await File.ReadAllTextAsync(backup));
        Assert.Contains(
            warnings,
            warning => warning.Contains("Sicherung", StringComparison.Ordinal)
                && warning.Contains("erhalten", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Austausch_vor_Stale_Move_bleibt_manuelle_Beilage()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatische Kopie");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var automatic = Assert.Single(
            (await collector.CollectAsync(request)).Attachments).SourcePath;
        var warnings = new List<string>();
        var guard = new ProjectWritePathGuard(_projectRoot);
        var ownership = DossierAttachmentOwnershipManifest.Load(
            AttachmentFolder(),
            guard,
            warnings);

        DossierAttachmentOwnershipManifest.Commit(
            AttachmentFolder(),
            guard,
            ownership,
            [],
            hasUnresolvedSelections: false,
            new DossierAttachmentPublishSession(warnings),
            warnings,
            CancellationToken.None,
            beforeStaleTargetStaged: path =>
                File.WriteAllText(path, "Manueller Austausch"));

        Assert.Equal("Manueller Austausch", await File.ReadAllTextAsync(automatic));
        Assert.Contains(warnings, warning => warning.Contains("fremde", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Beilagenordner_Sperre_serialisiert_zwei_Laeufe()
    {
        using var firstLocked = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        var folder = AttachmentFolder();
        var first = Task.Run(() =>
        {
            using var held = DossierAttachmentFolderLock.Acquire(
                folder,
                CancellationToken.None);
            firstLocked.Set();
            releaseFirst.Wait();
        });
        Assert.True(firstLocked.Wait(TimeSpan.FromSeconds(5)));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Run(() =>
        {
            using var blocked = DossierAttachmentFolderLock.Acquire(
                folder,
                cancellation.Token);
        }));

        releaseFirst.Set();
        await first;
        using var acquiredAfterRelease = DossierAttachmentFolderLock.Acquire(
            folder,
            CancellationToken.None);
    }

    [Fact]
    public async Task Abgewaehlte_automatische_Beilage_wird_entfernt_manuelle_bleibt()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var first = await collector.CollectAsync(request);
        var automatic = Assert.Single(first.Attachments).SourcePath;
        var manual = Path.Combine(AttachmentFolder(), "00_Offerte.pdf");
        await File.WriteAllTextAsync(manual, "Manuelle Beilage");

        await collector.CollectAsync(WithoutSelection(request));

        Assert.False(File.Exists(automatic));
        Assert.Equal("Automatisches Original", await File.ReadAllTextAsync(original));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(manual));
    }

    [Fact]
    public async Task Weiterhin_ausgewaehlte_Beilage_bleibt_bei_voruebergehendem_Fehler_erhalten()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Letzte gueltige Kopie");
        var (request, record) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        record.Protocol = null;
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var first = await collector.CollectAsync(request);
        var automatic = Assert.Single(first.Attachments).SourcePath;

        _originals.Clear();
        var failed = await collector.CollectAsync(request);

        Assert.Equal(DossierAttachmentKind.Missing, Assert.Single(failed.Attachments).Kind);
        Assert.Equal("Letzte gueltige Kopie", await File.ReadAllTextAsync(automatic));
        Assert.Contains(
            failed.Warnings,
            warning => warning.Contains("bleibt erhalten", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Gesperrte_abgewaehlte_Beilage_bleibt_fuer_spaeteren_Loeschversuch_markiert()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatische Kopie");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var automatic = Assert.Single(
            (await collector.CollectAsync(request)).Attachments).SourcePath;

        using (new FileStream(
                   automatic,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.None))
        {
            await collector.CollectAsync(WithoutSelection(request));
            Assert.True(File.Exists(automatic));
        }

        await collector.CollectAsync(WithoutSelection(request));

        Assert.False(File.Exists(automatic));
    }

    [Fact]
    public async Task Vorschau_uebernimmt_nach_Abwahl_nur_manuelle_Beilagen()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var first = await collector.CollectAsync(request);
        var automatic = Assert.Single(first.Attachments).SourcePath;
        var manual = Path.Combine(AttachmentFolder(), "00_QGIS_Plan.pdf");
        await File.WriteAllTextAsync(manual, "Manuelle Beilage");
        var temporaryDossier = Path.Combine(_root, "Vorschau", "Dossier");

        await collector.CollectIntoTemporaryAsync(
            WithoutSelection(request),
            temporaryDossier);

        var previewPdfs = DossierPdfAssemblyService.CollectAttachmentPdfs(temporaryDossier);
        var previewPdf = Assert.Single(previewPdfs);
        Assert.Equal("00_QGIS_Plan.pdf", Path.GetFileName(previewPdf));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(previewPdf));
        Assert.Equal("Automatisches Original", await File.ReadAllTextAsync(automatic));
    }

    [Fact]
    public async Task Veraenderte_automatische_Beilage_wird_als_manuell_bewahrt()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var first = await collector.CollectAsync(request);
        var changed = Assert.Single(first.Attachments).SourcePath;
        await File.WriteAllTextAsync(changed, "Von Hand geaendert");

        var result = await collector.CollectAsync(WithoutSelection(request));
        await collector.CollectAsync(WithoutSelection(request));

        Assert.Equal("Von Hand geaendert", await File.ReadAllTextAsync(changed));
        Assert.Contains(
            result.Warnings,
            warning => warning.Contains("ver\u00e4ndert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Gleichnamige_manuelle_Beilage_wird_nie_ueberschrieben()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var expectedAutomaticName = "01_TV_36080-36086.pdf";
        var manual = Path.Combine(AttachmentFolder(), expectedAutomaticName);
        await File.WriteAllTextAsync(manual, "Manuelle Beilage");
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(manual));
        var automatic = Assert.Single(result.Attachments);
        Assert.NotEqual(manual, automatic.SourcePath, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Automatisches Original", await File.ReadAllTextAsync(automatic.SourcePath));
        Assert.Equal(2, DossierPdfAssemblyService.CollectAttachmentPdfs(_dossierFolder).Count);
    }

    [Fact]
    public async Task Defektes_Eigentuemermanifest_stoppt_ohne_Dateien_zu_veraendern()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var first = await collector.CollectAsync(request);
        var automatic = Assert.Single(first.Attachments).SourcePath;
        var manual = Path.Combine(AttachmentFolder(), "00_Manuell.pdf");
        await File.WriteAllTextAsync(manual, "Manuelle Beilage");
        var manifest = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.json"));
        await File.WriteAllTextAsync(manifest, "{ kein gueltiges JSON");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => collector.CollectAsync(WithoutSelection(request)));

        Assert.Equal("Automatisches Original", await File.ReadAllTextAsync(automatic));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(manual));
        Assert.Equal("{ kein gueltiges JSON", await File.ReadAllTextAsync(manifest));
    }

    [Fact]
    public async Task Manifestpfad_darf_den_Beilagenordner_nicht_verlassen()
    {
        var original = Path.Combine(_root, "protokoll.pdf");
        await File.WriteAllTextAsync(original, "Automatisches Original");
        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var first = await collector.CollectAsync(request);
        var automatic = Assert.Single(first.Attachments).SourcePath;
        var manifest = Assert.Single(Directory.EnumerateFiles(AttachmentFolder(), "*.json"));
        var manifestText = await File.ReadAllTextAsync(manifest);
        manifestText = manifestText.Replace(
            Path.GetFileName(automatic),
            "../ausserhalb.pdf",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(manifest, manifestText);
        var outside = Path.Combine(_dossierFolder, "ausserhalb.pdf");
        await File.WriteAllTextAsync(outside, "Nicht anfassen");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => collector.CollectAsync(WithoutSelection(request)));

        Assert.Equal("Nicht anfassen", await File.ReadAllTextAsync(outside));
        Assert.True(File.Exists(automatic));
    }

    [Fact]
    public async Task Vorschau_sammelt_aktuell_in_Temp_und_laesst_den_Dossierordner_unveraendert()
    {
        var original = Path.Combine(_root, "aktuelles_protokoll.pdf");
        await File.WriteAllTextAsync(original, "Aktuelles Original");

        var echteBeilagen = Path.Combine(
            _dossierFolder,
            DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(echteBeilagen);
        var alterStand = Path.Combine(echteBeilagen, "01_TV_36080-36086.pdf");
        var manuelleBeilage = Path.Combine(echteBeilagen, "00_QGIS_Plan.pdf");
        await File.WriteAllTextAsync(alterStand, "Alter Stand");
        await File.WriteAllTextAsync(manuelleBeilage, "Manuelle Beilage");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { original }
            });
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var temporaeresDossier = Path.Combine(_root, "Vorschau", "Dossier");

        var result = await collector.CollectIntoTemporaryAsync(
            request,
            temporaeresDossier);

        var attachment = Assert.Single(result.Attachments);
        Assert.StartsWith(
            Path.GetFullPath(temporaeresDossier),
            attachment.SourcePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Aktuelles Original", await File.ReadAllTextAsync(attachment.SourcePath));
        Assert.Equal("Alter Stand", await File.ReadAllTextAsync(alterStand));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(manuelleBeilage));
        Assert.Equal("Aktuelles Original", await File.ReadAllTextAsync(original));

        var temporaererPlan = Path.Combine(
            temporaeresDossier,
            DossierFolderPlanner.AttachmentFolderName,
            Path.GetFileName(manuelleBeilage));
        Assert.Equal("Manuelle Beilage", await File.ReadAllTextAsync(temporaererPlan));
    }

    [Fact]
    public async Task Vorschau_weist_ein_Schreibziel_ausserhalb_des_System_Temp_Ordners_ab()
    {
        var (request, _) = BuildScenario(originals: new());
        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());
        var laufwerkswurzel = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        var unsicheresZiel = Path.Combine(
            laufwerkswurzel,
            "SewerStudio_DossierPreview_Unsicher_" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            collector.CollectIntoTemporaryAsync(request, unsicheresZiel));

        Assert.False(Directory.Exists(unsicheresZiel));
    }

    [Fact]
    public async Task Mehrere_Treffer_melden_welches_PDF_verwendet_wurde()
    {
        var a = Path.Combine(_root, "a.pdf");
        var b = Path.Combine(_root, "b.pdf");
        await File.WriteAllTextAsync(a, "A");
        await File.WriteAllTextAsync(b, "B");

        var (request, _) = BuildScenario(
            originals: new Dictionary<string, List<string>>
            {
                ["36080-36086"] = new() { a, b }
            });

        var collector = new DossierAttachmentCollector(
            new FakeLocator(_originals), new FakeProtocolPdf());

        var result = await collector.CollectAsync(request);

        Assert.Contains(result.Warnings, w => w.Contains("2 Protokoll-PDFs", StringComparison.Ordinal));
    }

    private (DossierExportRequest Request, HaltungRecord Record) BuildScenario(
        Dictionary<string, List<string>> originals)
    {
        var record = new HaltungRecord();
        record.Fields[FieldKeys.HoldingName] = "36080-36086";
        record.Fields[FieldKeys.HoldingLengthMeters] = "41.70";

        var project = new Project();
        project.Data.Add(record);

        var dossier = new DossierDefinition
        {
            Name = "Brämenhofstatt",
            HoldingIds = { record.Id }
        };

        var snapshot = DossierSnapshotBuilder.Build(dossier, project, new ProjectCostStore());

        var request = new DossierExportRequest(
            project,
            _projectRoot,
            new DossierAreaSettings(),
            dossier,
            snapshot,
            _dossierFolder);

        _originals = originals;
        return (request, record);
    }

    private string AttachmentFolder()
    {
        var folder = Path.Combine(
            _dossierFolder,
            DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static DossierExportRequest WithoutSelection(DossierExportRequest request)
    {
        var dossier = new DossierDefinition { Name = request.Dossier.Name };
        return request with
        {
            Dossier = dossier,
            Snapshot = DossierSnapshotBuilder.Build(
                dossier,
                request.Project,
                new ProjectCostStore())
        };
    }

    private Dictionary<string, List<string>> _originals = new();

    private sealed class FakeLocator : IInspectionProtocolFileLocator
    {
        private readonly Dictionary<string, List<string>> _lookup;

        public FakeLocator(Dictionary<string, List<string>> lookup) => _lookup = lookup;

        public List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
        {
            var name = record.GetFieldValue(FieldKeys.HoldingName) ?? "";
            return _lookup.TryGetValue(name, out var paths) ? paths : new List<string>();
        }

        public string? ResolveExistingPath(string? raw, string? projectPath) => null;

        public string? FindProtocolPath(
            HaltungRecord record,
            string? resolvedLink,
            string? initialFolder,
            string? projectPath,
            string? storedFilesRaw) => null;

        public void AddResolvedPdf(List<string> paths, string? raw, string projectFolder) { }

        public void ResolveSchachtPdfPaths(
            SchachtRecord schacht, string projectFolder, List<string> paths)
        { }
    }

    private sealed class FakeProtocolPdf : IProtocolPdfExporter
    {
        public byte[] BuildPdf(string projectTitle, ProtocolDocument document, string projectRootAbs)
            => new byte[] { 1, 2, 3 };

        public byte[] BuildPdf(
            string projectTitle,
            ProtocolDocument document,
            string projectRootAbs,
            ProtocolPdfExportOptions options) => new byte[] { 1, 2, 3 };

        public byte[] BuildHaltungsprotokollPdf(
            Project project,
            HaltungRecord record,
            ProtocolDocument document,
            string projectRootAbs,
            HaltungsprotokollPdfOptions? options = null) => new byte[] { 1, 2, 3 };

        public byte[] BuildCsv(ProtocolDocument document, ProtocolPdfExportOptions? options = null)
            => Array.Empty<byte>();
    }
}
