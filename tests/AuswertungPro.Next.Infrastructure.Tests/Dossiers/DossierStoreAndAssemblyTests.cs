using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Media;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using UglyToad.PdfPig;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

public sealed class DossierFileStoreTests : IDisposable
{
    private readonly string _projectRoot = Path.Combine(
        Path.GetTempPath(), "dossier_store_" + Guid.NewGuid().ToString("N"));

    public DossierFileStoreTests() => Directory.CreateDirectory(_projectRoot);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_projectRoot))
                Directory.Delete(_projectRoot, recursive: true);
        }
        catch
        {
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Erstlauf_ohne_Datei_ergibt_ein_leeres_Dokument()
    {
        var store = new DossierFileStore();

        var document = await store.LoadAsync(_projectRoot);

        Assert.Empty(document.Dossiers);
        Assert.Equal(DossierDocument.CurrentSchemaVersion, document.SchemaVersion);
    }

    [Fact]
    public async Task Speichern_und_Laden_erhaelt_die_Auswahl()
    {
        var store = new DossierFileStore();
        var id = Guid.NewGuid();

        var document = new DossierDocument
        {
            Area = new DossierAreaSettings { AreaTitle = "Erstfeld West" },
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Brämenhofstatt 3+4",
                    ParcelNumbers = "762+756",
                    HoldingIds = { id },
                    Status = DossierStatus.Versendet
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);
        var geladen = await store.LoadAsync(_projectRoot);

        var dossier = Assert.Single(geladen.Dossiers);
        Assert.Equal("Brämenhofstatt 3+4", dossier.Name);
        Assert.Equal("762+756", dossier.ParcelNumbers);
        Assert.Equal(id, Assert.Single(dossier.HoldingIds));
        Assert.Equal(DossierStatus.Versendet, dossier.Status);
        Assert.Equal("Erstfeld West", geladen.Area.AreaTitle);
    }

    [Fact]
    public async Task Speichern_einer_neuen_Liegenschaft_legt_ihren_Ordner_sofort_an()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Liegenschaft Nr. 439 Dittli",
                    FolderName = "Liegenschaft Nr. 439 Dittli"
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        Assert.True(Directory.Exists(Path.Combine(
            DossierFolderPlanner.ResolveRoot(_projectRoot),
            "Liegenschaft Nr. 439 Dittli")));
    }

    [Fact]
    public async Task Neue_Liegenschaft_erhaelt_das_feste_Zustandsklassenblatt_bytegleich()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 1, 2, 3, 4 };
        var store = new DossierFileStore(new FixedConditionClassPdfService(pdf));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Liegenschaft 439",
                    FolderName = "Liegenschaft 439"
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        var target = Path.Combine(
            DossierFolderPlanner.ResolveDossierFolder(_projectRoot, "Liegenschaft 439"),
            DossierFolderPlanner.ConditionClassPdfFileName);
        Assert.Equal(pdf, await File.ReadAllBytesAsync(target));
    }

    [Fact]
    public async Task Stapelanlage_legt_das_Zustandsklassenblatt_in_jeden_neuen_Ordner()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 5, 6, 7, 8 };
        var store = new DossierFileStore(new FixedConditionClassPdfService(pdf));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Erste", FolderName = "Erste" },
                new DossierDefinition { Name = "Zweite", FolderName = "Zweite" }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        foreach (var folderName in new[] { "Erste", "Zweite" })
        {
            var target = Path.Combine(
                DossierFolderPlanner.ResolveDossierFolder(_projectRoot, folderName),
                DossierFolderPlanner.ConditionClassPdfFileName);
            Assert.Equal(pdf, await File.ReadAllBytesAsync(target));
        }
    }

    [Fact]
    public async Task Neue_Liegenschaft_erzeugt_Haltungsliste_nicht_automatisch()
    {
        var conditionPdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 1, 1, 1 };
        var listPdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 2, 2, 2 };
        var listService = new FixedHoldingListPdfService(listPdf);
        var store = new DossierFileStore(
            new FixedConditionClassPdfService(conditionPdf),
            listService);

        var holding = new HaltungRecord();
        holding.Fields[FieldKeys.HoldingName] = "77467-77463";
        holding.Fields[FieldKeys.PipeMaterial] = "Beton";
        holding.Fields[FieldKeys.NominalDiameterMm] = "300";
        holding.Fields[FieldKeys.HoldingLengthMeters] = "3.60";
        holding.Fields[FieldKeys.ConditionClass] = "2";
        holding.Fields[FieldKeys.UsageType] = "Mischabwasser";
        var project = new Project();
        project.Data.Add(holding);

        var dossier = new DossierDefinition
        {
            Name = "Feldliweg 26",
            FolderName = "Feldliweg 26",
            OwnerName = "Heinz Müller",
            Address = "Feldliweg",
            HouseNumbers = "26",
            PostalCode = "6460",
            Town = "Altdorf",
            HoldingIds = { holding.Id }
        };
        var document = new DossierDocument { Dossiers = { dossier } };

        await store.SaveAsync(_projectRoot, document, project);

        var folder = DossierFolderPlanner.ResolveDossierFolder(_projectRoot, dossier.FolderName);
        Assert.Equal(conditionPdf, await File.ReadAllBytesAsync(Path.Combine(
            folder,
            DossierFolderPlanner.ConditionClassPdfFileName)));
        Assert.False(File.Exists(Path.Combine(
            folder,
            DossierFolderPlanner.HoldingListPdfFileName)));
        Assert.Empty(listService.Models);
    }

    [Fact]
    public async Task Alter_Speicheraufruf_ohne_Projektstand_bleibt_kompatibel()
    {
        var store = new DossierFileStore(
            conditionClassPdf: null,
            holdingListPdf: new FixedHoldingListPdfService([1, 2, 3]));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Neu", FolderName = "Neu" }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        Assert.True(File.Exists(DossierFolderPlanner.ResolveDocumentPath(_projectRoot)));
        Assert.True(Directory.Exists(
            DossierFolderPlanner.ResolveDossierFolder(_projectRoot, "Neu")));
        Assert.False(File.Exists(Path.Combine(
            DossierFolderPlanner.ResolveDossierFolder(_projectRoot, "Neu"),
            DossierFolderPlanner.HoldingListPdfFileName)));
    }

    [Fact]
    public async Task Laden_zieht_einen_geloeschten_Ordner_nur_mit_festem_Blatt_nach()
    {
        var conditionPdf = new byte[] { 1, 2, 3, 4 };
        var listPdf = new byte[] { 5, 6, 7, 8 };
        var listService = new FixedHoldingListPdfService(listPdf);
        var store = new DossierFileStore(
            new FixedConditionClassPdfService(conditionPdf),
            listService);
        var project = new Project();
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Nachziehen", FolderName = "Nachziehen" }
            }
        };
        await store.SaveAsync(_projectRoot, document, project);
        var folder = DossierFolderPlanner.ResolveDossierFolder(_projectRoot, "Nachziehen");
        Directory.Delete(folder, recursive: true);
        listService.Models.Clear();

        await store.LoadAsync(_projectRoot, project);

        Assert.Equal(conditionPdf, await File.ReadAllBytesAsync(Path.Combine(
            folder,
            DossierFolderPlanner.ConditionClassPdfFileName)));
        Assert.False(File.Exists(Path.Combine(
            folder,
            DossierFolderPlanner.HoldingListPdfFileName)));
        Assert.Empty(listService.Models);
    }

    [Fact]
    public async Task Vorhandene_Haltungsliste_wird_nicht_ueberschrieben()
    {
        var folderName = "Bestehend mit Liste";
        var folder = DossierFolderPlanner.ResolveDossierFolder(_projectRoot, folderName);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, DossierFolderPlanner.HoldingListPdfFileName);
        var userFile = new byte[] { 9, 8, 7, 6 };
        await File.WriteAllBytesAsync(target, userFile);
        var listService = new FixedHoldingListPdfService([1, 2, 3, 4]);
        var store = new DossierFileStore(conditionClassPdf: null, holdingListPdf: listService);
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = folderName, FolderName = folderName }
            }
        };

        await store.SaveAsync(_projectRoot, document, new Project());

        Assert.Equal(userFile, await File.ReadAllBytesAsync(target));
        Assert.Empty(listService.Models);
    }

    [Fact]
    public async Task Vorhandenes_Zustandsklassenblatt_wird_nicht_ueberschrieben()
    {
        var folderName = "Bestehend";
        var folder = DossierFolderPlanner.ResolveDossierFolder(_projectRoot, folderName);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, DossierFolderPlanner.ConditionClassPdfFileName);
        var benutzerdatei = new byte[] { 9, 8, 7, 6 };
        await File.WriteAllBytesAsync(target, benutzerdatei);

        var store = new DossierFileStore(
            new FixedConditionClassPdfService(new byte[] { 1, 2, 3, 4 }));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = folderName, FolderName = folderName }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        Assert.Equal(benutzerdatei, await File.ReadAllBytesAsync(target));
    }

    [Fact]
    public async Task Laden_zieht_fehlende_Ordner_vorhandener_Liegenschaften_nach()
    {
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 4, 3, 9 };
        var store = new DossierFileStore(new FixedConditionClassPdfService(pdf));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Liegenschaft Nr. 439 Dittli",
                    FolderName = "Liegenschaft Nr. 439 Dittli"
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        var dossierFolder = DossierFolderPlanner.ResolveDossierFolder(
            _projectRoot,
            "Liegenschaft Nr. 439 Dittli");
        Directory.Delete(dossierFolder, recursive: true);

        var jsonPath = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        var jsonVorher = await File.ReadAllBytesAsync(jsonPath);

        var geladen = await store.LoadAsync(_projectRoot);

        Assert.Single(geladen.Dossiers);
        Assert.True(Directory.Exists(dossierFolder));
        Assert.Equal(
            pdf,
            await File.ReadAllBytesAsync(Path.Combine(
                dossierFolder,
                DossierFolderPlanner.ConditionClassPdfFileName)));
        Assert.Equal(jsonVorher, await File.ReadAllBytesAsync(jsonPath));
    }

    [Fact]
    public async Task Fehlende_feste_PDF_Vorlage_hinterlaesst_weder_Json_noch_Dossierordner()
    {
        var missingTemplate = Path.Combine(
            _projectRoot,
            "fehlt",
            DossierFolderPlanner.ConditionClassPdfFileName);
        var store = new DossierFileStore(
            new DossierConditionClassPdfTemplateService(missingTemplate));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Neu", FolderName = "Neu" }
            }
        };

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => store.SaveAsync(_projectRoot, document));

        Assert.False(File.Exists(DossierFolderPlanner.ResolveDocumentPath(_projectRoot)));
        Assert.False(Directory.Exists(
            DossierFolderPlanner.ResolveDossierFolder(_projectRoot, "Neu")));
    }

    [Fact]
    public async Task Ungueltiger_Ordnername_macht_eine_lesbare_Json_nicht_zu_einer_bad_Datei()
    {
        var dossierRoot = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(dossierRoot);

        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Unsicher",
                    FolderName = Path.Combine("..", "Ausbruch")
                }
            }
        };
        var jsonPath = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(document));

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => new DossierFileStore().LoadAsync(_projectRoot));

        Assert.Contains("Liegenschaftsordner", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(dossierRoot, "dossiers.json.bad_*"));
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "Ausbruch")));
    }

    [Fact]
    public async Task Dossierordner_darf_den_Sammelordner_nicht_verlassen()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    Name = "Unsicher",
                    FolderName = Path.Combine("..", "Ausbruch")
                }
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SaveAsync(_projectRoot, document));

        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "Ausbruch")));
        Assert.False(File.Exists(DossierFolderPlanner.ResolveDocumentPath(_projectRoot)));
    }

    [Fact]
    public async Task Speicherfehler_entfernt_nur_den_neuen_leeren_Dossierordner()
    {
        var dossierRoot = DossierFolderPlanner.ResolveRoot(_projectRoot);
        var bestehenderOrdner = Path.Combine(dossierRoot, "Bestehend");
        Directory.CreateDirectory(bestehenderOrdner);
        await File.WriteAllTextAsync(Path.Combine(bestehenderOrdner, "behalten.txt"), "wichtig");

        // Ein Ordner am Ort der JSON-Datei erzwingt einen Schreibfehler erst,
        // nachdem die Liegenschaftsordner angelegt worden sind.
        Directory.CreateDirectory(DossierFolderPlanner.ResolveDocumentPath(_projectRoot));

        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Bestehend", FolderName = "Bestehend" },
                new DossierDefinition { Name = "Neu", FolderName = "Neu" }
            }
        };

        await Assert.ThrowsAnyAsync<Exception>(
            () => new DossierFileStore().SaveAsync(_projectRoot, document));

        Assert.True(File.Exists(Path.Combine(bestehenderOrdner, "behalten.txt")));
        Assert.False(Directory.Exists(Path.Combine(dossierRoot, "Neu")));
    }

    [Fact]
    public async Task Speicherfehler_nimmt_eigenes_Zustandsklassenblatt_und_neuen_Ordner_zurueck()
    {
        var dossierRoot = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(DossierFolderPlanner.ResolveDocumentPath(_projectRoot));

        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Neu", FolderName = "Neu" }
            }
        };
        var store = new DossierFileStore(
            new FixedConditionClassPdfService(
                new byte[] { 0x25, 0x50, 0x44, 0x46, 9, 9, 9, 9 }));

        await Assert.ThrowsAnyAsync<Exception>(
            () => store.SaveAsync(_projectRoot, document));

        Assert.False(Directory.Exists(Path.Combine(dossierRoot, "Neu")));
    }

    [Fact]
    public async Task Speicherfehler_nimmt_festes_Blatt_und_neuen_Ordner_zurueck()
    {
        var dossierRoot = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(DossierFolderPlanner.ResolveDocumentPath(_projectRoot));
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition { Name = "Neu", FolderName = "Neu" }
            }
        };
        var store = new DossierFileStore(
            new FixedConditionClassPdfService([1, 2, 3, 4]),
            new FixedHoldingListPdfService([5, 6, 7, 8]));

        await Assert.ThrowsAnyAsync<Exception>(
            () => store.SaveAsync(_projectRoot, document, new Project()));

        Assert.False(Directory.Exists(Path.Combine(dossierRoot, "Neu")));
    }

    [Fact]
    public async Task Sichere_Ruecknahme_bewahrt_eine_inzwischen_geaenderte_Datei()
    {
        var path = Path.Combine(_projectRoot, "geaendert.pdf");
        var urspruenglich = new byte[] { 1, 2, 3, 4 };
        var geaendert = new byte[] { 9, 8, 7, 6 };
        await File.WriteAllBytesAsync(path, geaendert);

        var geloescht = DossierOwnedFileRollback.DeleteIfSha256Matches(
            path,
            SHA256.HashData(urspruenglich));

        Assert.False(geloescht);
        Assert.Equal(geaendert, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task Zweites_Speichern_legt_ein_Backup_an()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument();

        await store.SaveAsync(_projectRoot, document);
        document.Dossiers.Add(new DossierDefinition { Name = "Zweiter Stand" });
        await store.SaveAsync(_projectRoot, document);

        var backup = DossierFolderPlanner.ResolveDocumentPath(_projectRoot) + ".bak";
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public async Task Kaputte_Datei_wird_aus_dem_Backup_gerettet()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument
        {
            Dossiers = { new DossierDefinition { Name = "Guter Stand" } }
        };

        await store.SaveAsync(_projectRoot, document);

        var path = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        File.Copy(path, path + ".bak", overwrite: true);
        await File.WriteAllTextAsync(path, "{ das ist kein JSON");

        var geladen = await store.LoadAsync(_projectRoot);

        Assert.Equal("Guter Stand", Assert.Single(geladen.Dossiers).Name);
    }

    [Fact]
    public async Task Kaputte_Datei_ohne_Backup_bricht_ab_statt_leer_weiterzumachen()
    {
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        var path = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);
        await File.WriteAllTextAsync(path, "{ kaputt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.LoadAsync(_projectRoot));

        // Nichts wurde ueberschrieben — die Originaldatei liegt unveraendert da.
        Assert.Equal("{ kaputt", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Die_abgeleitete_Regel_HasContent_wird_nicht_mitgespeichert()
    {
        // HasContent ist eine reine Rechenregel ("ist diese Zeile leer?"),
        // keine Angabe. Landet sie in dossiers.json, steht dort ein Wert, den
        // niemand gepflegt hat und der beim Lesen ignoriert wird — Ballast,
        // der bei einer spaeteren Aenderung der Regel auch noch falsch waere.
        var store = new DossierFileStore();
        var document = new DossierDocument();
        var dossier = new DossierDefinition { Name = "Liegenschaft 439" };
        dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "30",
            ParcelNumber = "439",
            Name = "Kurt Beispiel"
        });
        document.Dossiers.Add(dossier);

        await store.SaveAsync(_projectRoot, document);

        var json = await File.ReadAllTextAsync(
            DossierFolderPlanner.ResolveDocumentPath(_projectRoot));

        Assert.DoesNotContain("HasContent", json, StringComparison.Ordinal);
        Assert.Contains("Kurt Beispiel", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Neue_Datei_speichert_nur_die_gemeinsamen_Verzeichniseintraege()
    {
        var store = new DossierFileStore();
        var document = new DossierDocument
        {
            Dossiers =
            {
                new DossierDefinition
                {
                    TocAttachments =
                    {
                        new DossierTocAttachment
                        {
                            Title = "TV-Protokolle",
                            PageNumber = "8"
                        }
                    }
                }
            }
        };

        await store.SaveAsync(_projectRoot, document);

        var json = await File.ReadAllTextAsync(
            DossierFolderPlanner.ResolveDocumentPath(_projectRoot));

        Assert.Contains("TocAttachments", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TocAttachmentLines", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TocAttachmentPageNumbers", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Neuere_Formatversion_wird_nicht_erraten()
    {
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            DossierFolderPlanner.ResolveDocumentPath(_projectRoot),
            """{ "SchemaVersion": 99, "Dossiers": [] }""");

        // DossierSchemaVersionException ist eine InvalidOperationException,
        // aber ein eigener Typ (siehe W5) — hier ausdruecklich der genaue Typ.
        await Assert.ThrowsAsync<DossierSchemaVersionException>(
            () => store.LoadAsync(_projectRoot));
    }

    [Fact]
    public async Task Neuere_Formatversion_wird_nicht_durch_ein_Backup_umgangen()
    {
        // Genau das Fehlerszenario aus dem Fix-Brief: eine spaetere
        // Programmversion hat Version 3 geschrieben, es liegt aber noch ein
        // gueltiges .bak mit dem alten Stand daneben. Die aeltere
        // Programmversion darf das Backup NICHT still laden — sonst
        // ueberschreibt die naechste Aenderung die Version-3-Datei mit
        // Version-2-Inhalt.
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        var path = DossierFolderPlanner.ResolveDocumentPath(_projectRoot);

        await File.WriteAllTextAsync(
            path + ".bak",
            """{ "SchemaVersion": 2, "Dossiers": [] }""");
        await File.WriteAllTextAsync(
            path,
            """{ "SchemaVersion": 99, "Dossiers": [] }""");

        await Assert.ThrowsAsync<DossierSchemaVersionException>(
            () => store.LoadAsync(_projectRoot));
    }

    [Fact]
    public async Task Eine_echte_Version_1_Datei_wird_beim_Laden_umgestellt()
    {
        // Prueft die Verdrahtung Store -> Umstellung: bisher gab es nur
        // Tests fuer die reine Umstellungslogik und fuer den Store getrennt.
        var store = new DossierFileStore();
        var root = DossierFolderPlanner.ResolveRoot(_projectRoot);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            DossierFolderPlanner.ResolveDocumentPath(_projectRoot),
            """
            {
              "SchemaVersion": 1,
              "Dossiers": [
                {
                  "Name": "Brämenhofstatt 3",
                  "HouseNumbers": "3",
                  "OwnerName": "Martin Muster",
                  "ContactPhone": "079 858 53 74"
                }
              ]
            }
            """);

        var geladen = await store.LoadAsync(_projectRoot);

        var dossier = Assert.Single(geladen.Dossiers);
        var row = Assert.Single(dossier.Owners);
        Assert.Equal("3", row.HouseNumber);
        Assert.Equal("Martin Muster", row.Name);
        Assert.Equal("079 858 53 74", row.Phone);
        Assert.Equal(DossierDocument.CurrentSchemaVersion, geladen.SchemaVersion);
    }

    private sealed class FixedConditionClassPdfService(byte[] pdf)
        : IDossierConditionClassPdfService
    {
        private readonly byte[] _pdf = (byte[])pdf.Clone();

        public byte[] CreatePdf() => (byte[])_pdf.Clone();
    }

    private sealed class FixedHoldingListPdfService(byte[] pdf)
        : IDossierHoldingListPdfService
    {
        private readonly byte[] _pdf = (byte[])pdf.Clone();

        public List<DossierHoldingListPdfModel> Models { get; } = new();

        public byte[] CreatePdf(DossierHoldingListPdfModel model)
        {
            Models.Add(model);
            return (byte[])_pdf.Clone();
        }
    }
}

public sealed class DossierPdfAssemblyServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "dossier_pdf_" + Guid.NewGuid().ToString("N"));

    public DossierPdfAssemblyServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // Aufraeumfehler darf den Test nicht rot machen.
        }
    }

    [Fact]
    public async Task Ohne_Word_Datei_wird_klar_gemeldet_statt_ein_Teil_PDF_zu_bauen()
    {
        var service = new DossierPdfAssemblyService(
            new FakeMerge(), (_, _) => true);

        var result = await service.AssembleAsync(_folder);

        Assert.False(result.Success);
        Assert.Contains("Word erzeugen", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ohne_verfuegbares_Office_entsteht_kein_unvollstaendiges_PDF()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var service = new DossierPdfAssemblyService(
            new FakeMerge(), (_, _) => false);

        var result = await service.AssembleAsync(_folder);

        Assert.False(result.Success);
        Assert.Contains("Microsoft Word", result.Message, StringComparison.Ordinal);
        Assert.Contains("LibreOffice", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(
            Path.Combine(_folder, DossierFolderPlanner.CombinedPdfFileName)));
    }

    [Fact]
    public void LibreOffice_uebernimmt_wenn_Microsoft_Word_nicht_umwandeln_kann()
    {
        var versuche = new List<string>();

        var result = DossierWordPdfConverter.TryConvertToPdf(
            "Dossier.docx",
            "Dossier.pdf",
            (_, _) =>
            {
                versuche.Add("Word");
                return false;
            },
            (_, _) =>
            {
                versuche.Add("LibreOffice");
                return true;
            });

        Assert.True(result);
        Assert.Equal(new[] { "Word", "LibreOffice" }, versuche);
    }

    [Fact]
    public void Nach_erfolgreichem_Microsoft_Word_wird_LibreOffice_nicht_gestartet()
    {
        var libreOfficeGestartet = false;

        var result = DossierWordPdfConverter.TryConvertToPdf(
            "Dossier.docx",
            "Dossier.pdf",
            (_, _) => true,
            (_, _) =>
            {
                libreOfficeGestartet = true;
                return true;
            });

        Assert.True(result);
        Assert.False(libreOfficeGestartet);
    }

    [Fact]
    public void Microsoft_Word_exportiert_die_Word_Textmarken_als_PDF_Lesezeichen()
    {
        var pdfPath = @"C:\Dossier Ordner\Eigentuemerdossier.pdf";

        var arguments = WordInterop.CreateExportAsFixedFormatArguments(pdfPath);

        Assert.Equal(11, arguments.Length);
        Assert.Equal(pdfPath, arguments[0]);
        Assert.Equal(17, arguments[1]);
        Assert.All(arguments.Skip(2).Take(8), argument => Assert.Same(Type.Missing, argument));
        Assert.Equal(2, arguments[10]);
    }

    [Fact]
    public void LibreOffice_erhaelt_sichere_einzelne_Argumente_und_ein_eigenes_Profil()
    {
        var wordPath = @"C:\Dossier Ordner\Eigentümerdossier.docx";
        var outputFolder = @"C:\Ausgabe Ordner";
        var startInfo = LibreOfficeWriterPdfConverter.CreateStartInfo(
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            wordPath,
            outputFolder,
            @"C:\Temporärer Ordner\Profil");
        var arguments = startInfo.ArgumentList.ToArray();

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Contains("--headless", arguments);
        Assert.Contains(wordPath, arguments);
        Assert.Contains(outputFolder, arguments);
        Assert.Contains(arguments, argument =>
            argument.StartsWith("-env:UserInstallation=file:///", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fuehrt_Word_PDF_und_Beilagen_in_Dateinamen_Reihenfolge_zusammen()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var beilagen = Path.Combine(_folder, DossierFolderPlanner.AttachmentFolderName);
        Directory.CreateDirectory(beilagen);
        await File.WriteAllBytesAsync(
            Path.Combine(beilagen, "02_TV_zweite.pdf"),
            CreatePdf("ZWEITE BEILAGE"));
        await File.WriteAllBytesAsync(
            Path.Combine(beilagen, "01_Uebersichtsplan.pdf"),
            CreatePdf("ERSTE BEILAGE"));

        var merge = new FakeMerge();
        var service = new DossierPdfAssemblyService(
            merge,
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, CreatePdf("WORD-DOSSIER"));
                return true;
            });

        var result = await service.AssembleAsync(_folder);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(
            Path.Combine(_folder, DossierFolderPlanner.CombinedPdfFileName)));

        Assert.Equal(2, merge.LastOriginals.Count);
        Assert.EndsWith("01_Uebersichtsplan.pdf", merge.LastOriginals[0], StringComparison.Ordinal);
        Assert.EndsWith("02_TV_zweite.pdf", merge.LastOriginals[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fuegt_das_Zustandsklassenblatt_auch_ohne_weitere_Beilage_ein()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var service = new DossierPdfAssemblyService(
            new PdfMergeService(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, CreatePdf("WORD-DOSSIER"));
                return true;
            });

        var result = await service.AssembleAsync(_folder);

        Assert.True(result.Success, result.Message);
        Assert.Contains("Erkläranhang", result.Message, StringComparison.Ordinal);
        using var document = PdfDocument.Open(result.FilePath!);
        Assert.Equal(2, document.NumberOfPages);
        Assert.Contains("WORD-DOSSIER", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Zustandsklassen", document.GetPage(2).Text, StringComparison.Ordinal);
        Assert.Contains("Sofort", document.GetPage(2).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Das_Erklaerblatt_bleibt_auch_bei_Abwahl_aller_Blaetter_erhalten()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "Eigentuemerdossier.docx"), "Platzhalter");

        var service = new DossierPdfAssemblyService(
            new PdfMergeService(),
            (_, pdfPath) =>
            {
                File.WriteAllBytes(pdfPath!, CreatePdf("WORD-DOSSIER"));
                return true;
            });

        var result = await service.AssembleAsync(
            _folder,
            (_, _) => Task.FromResult<IReadOnlySet<int>?>(new HashSet<int> { 1, 2, 3 }));

        Assert.True(result.Success, result.Message);
        using var document = PdfDocument.Open(result.FilePath!);
        Assert.Equal(1, document.NumberOfPages);
        Assert.Contains("Zustandsklassen", document.GetPage(1).Text, StringComparison.Ordinal);
        Assert.Contains("Sofort", document.GetPage(1).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nimmt_die_zuletzt_bearbeitete_Word_Datei()
    {
        var alt = Path.Combine(_folder, "Eigentuemerdossier.docx");
        var neu = Path.Combine(_folder, "Eigentuemerdossier-2.docx");
        await File.WriteAllTextAsync(alt, "alt");
        await File.WriteAllTextAsync(neu, "neu");
        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(neu, DateTime.UtcNow);

        string? verwendet = null;
        var service = new DossierPdfAssemblyService(
            new FakeMerge(),
            (wordPath, pdfPath) =>
            {
                verwendet = wordPath;
                File.WriteAllBytes(pdfPath!, new byte[] { 1 });
                return true;
            });

        await service.AssembleAsync(_folder);

        Assert.Equal(neu, verwendet);
    }

    private sealed class FakeMerge : IPdfMergeService
    {
        private readonly PdfMergeService _inner = new();

        public List<string> LastOriginals { get; private set; } = new();

        public byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
        {
            LastOriginals = new List<string>(originalPdfPaths);
            return _inner.MergeWithOriginals(generatedPdf, originalPdfPaths);
        }

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
        {
            LastOriginals = new List<string>(originalPdfPaths);
            return _inner.MergeOriginals(originalPdfPaths);
        }
    }

    private static byte[] CreatePdf(string text)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.Content().Text(text);
            });
        }).GeneratePdf();
    }
}
