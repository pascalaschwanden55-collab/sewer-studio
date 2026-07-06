using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfEntryResolverTests
{
    [Fact]
    public void ResolveEntriesForExport_does_not_merge_original_photo_into_current_entry()
    {
        var record = new HaltungRecord();
        var current = new ProtocolEntry
        {
            Code = "BCCYB",
            MeterStart = 3.569,
            Beschreibung = "Bogen nach unten"
        };
        var original = new ProtocolEntry
        {
            Code = "BCCYB",
            MeterStart = 3.569,
            Beschreibung = "Bogen nach unten"
        };
        original.FotoPaths.Add("Fotos/Haltungen/H1/foto.jpg");

        var doc = new ProtocolDocument
        {
            Original = new ProtocolRevision
            {
                Entries = { original }
            },
            Current = new ProtocolRevision
            {
                Entries = { current }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        var entry = Assert.Single(entries);
        Assert.Same(current, entry);
        Assert.Empty(entry.FotoPaths);
        Assert.Empty(current.FotoPaths);
    }

    [Fact]
    public void ResolveEntriesForExport_does_not_restore_photos_from_baselines()
    {
        var record = new HaltungRecord();
        var current = new ProtocolEntry
        {
            Code = "BCD",
            MeterStart = 0,
            Beschreibung = "Rohranfang"
        };
        var original = new ProtocolEntry
        {
            Code = "BCD",
            MeterStart = 0,
            Beschreibung = "Rohranfang"
        };
        original.FotoPaths.Add("Fotos/Haltungen/H1/original.jpg");
        var history = new ProtocolEntry
        {
            Code = "BCD",
            MeterStart = 0,
            Beschreibung = "Rohranfang"
        };
        history.FotoPaths.Add("Fotos/Haltungen/H1/history.jpg");

        var doc = new ProtocolDocument
        {
            Original = new ProtocolRevision
            {
                Entries = { original }
            },
            Current = new ProtocolRevision
            {
                Entries = { current }
            },
            History =
            {
                new ProtocolRevision { Entries = { history } }
            }
        };

        var entry = Assert.Single(ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc));

        Assert.Empty(entry.FotoPaths);
        Assert.Empty(current.FotoPaths);
    }

    [Fact]
    public void ResolveEntriesForExport_merges_finding_photo_into_existing_entry()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAB",
            MeterStart = 1.2,
            Raw = "Riss",
            FotoPath = "Fotos/finding.jpg"
        });

        var existing = new ProtocolEntry
        {
            Code = "BAB",
            MeterStart = 1.2,
            Beschreibung = "Riss"
        };
        existing.FotoPaths.Add("Fotos/original.jpg");

        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { existing }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        var entry = Assert.Single(entries);
        Assert.Same(existing, entry);
        Assert.Equal(new[] { "Fotos/original.jpg", "Fotos/finding.jpg" }, entry.FotoPaths);
    }

    [Fact]
    public void ResolveEntriesForExport_does_not_merge_stale_import_photo_when_central_photo_exists()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAA",
            MeterStart = 1.2,
            Raw = "Ablagerung",
            FotoPath = @"D:\Projekt\Importdateien\XTF\Foto\H_06-001_002.jpg"
        });

        var existing = new ProtocolEntry
        {
            Code = "BAA",
            MeterStart = 1.2,
            Beschreibung = "Ablagerung"
        };
        existing.FotoPaths.Add("Fotos/Haltungen/06-001/H_06-001_002.jpg");

        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { existing }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        var entry = Assert.Single(entries);
        Assert.Same(existing, entry);
        Assert.Equal(new[] { "Fotos/Haltungen/06-001/H_06-001_002.jpg" }, entry.FotoPaths);
    }

    [Fact]
    public void ResolveEntriesForExport_does_not_merge_stale_import_photo_when_holding_photo_was_renamed()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAA",
            MeterStart = 1.2,
            Raw = "Ablagerung",
            FotoPath = @"D:\Projekt\Importdateien\XTF\Foto\H_22147-547.01_116.jpg"
        });

        var existing = new ProtocolEntry
        {
            Code = "BAA",
            MeterStart = 1.2,
            Beschreibung = "Ablagerung"
        };
        existing.FotoPaths.Add("Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg");

        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { existing }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        var entry = Assert.Single(entries);
        Assert.Same(existing, entry);
        Assert.Equal(new[] { "Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg" }, entry.FotoPaths);
    }

    [Fact]
    public void ResolveEntriesForExport_skips_imported_finding_when_user_deleted_matching_entry()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAC",
            MeterStart = 2.5,
            Raw = "Bruch"
        });

        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry
                    {
                        Code = "BAC",
                        MeterStart = 2.5,
                        Beschreibung = "Bruch",
                        IsDeleted = true
                    }
                }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        Assert.Empty(entries);
    }

    [Fact]
    public void ResolveEntriesForExport_uses_primary_damage_fallback_when_no_findings_exist()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Primaere_Schaeden", "BAB @ 1,20 m (Riss)\n... ignoriert", FieldSource.Manual, userEdited: false);

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, new ProtocolDocument());

        var entry = Assert.Single(entries);
        Assert.Equal("BAB", entry.Code);
        Assert.Equal(1.2, entry.MeterStart);
        Assert.Equal("Riss", entry.Beschreibung);
        Assert.Equal(ProtocolEntrySource.Imported, entry.Source);
    }

    [Fact]
    public void ResolveEntriesForExport_nutzt_schadenlage_nicht_als_meterende()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAIZ",
            MeterStart = 0.6,
            SchadenlageAnfang = 12,
            SchadenlageEnde = 12,
            Raw = "Einragendes Dichtungsmaterial",
            FotoPath = "Fotos/Haltungen/H1/L_H1_003.jpg"
        });

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, new ProtocolDocument());

        var entry = Assert.Single(entries);
        Assert.Equal(0.6, entry.MeterStart);
        Assert.Null(entry.MeterEnd);
        Assert.False(entry.IsStreckenschaden);
        Assert.Equal("12", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("12", entry.CodeMeta.Parameters["vsa.uhr.bis"]);
    }

    [Fact]
    public void ResolveEntriesForExport_repariert_gespeichertes_uhrlage_meterende()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAIZ",
            MeterStart = 0.6,
            SchadenlageAnfang = 12,
            SchadenlageEnde = 12,
            Raw = "Einragendes Dichtungsmaterial"
        });

        var existing = new ProtocolEntry
        {
            Code = "BAIZ",
            Beschreibung = "Einragendes Dichtungsmaterial",
            MeterStart = 0.6,
            MeterEnd = 12,
            IsStreckenschaden = true,
            Source = ProtocolEntrySource.Imported
        };
        var doc = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { existing }
            }
        };

        var entries = ProtocolPdfEntryResolver.ResolveEntriesForExport(record, doc);

        var entry = Assert.Single(entries);
        Assert.Same(existing, entry);
        Assert.Equal(0.6, entry.MeterStart);
        Assert.Null(entry.MeterEnd);
        Assert.False(entry.IsStreckenschaden);
        Assert.Equal("12", entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("12", entry.CodeMeta.Parameters["vsa.uhr.bis"]);
    }

    [Fact]
    public void ResolveHoldingLength_prefers_record_length_before_entry_maximum()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungslaenge_m", "42,5", FieldSource.Manual, userEdited: false);
        var entries = new[]
        {
            new ProtocolEntry { MeterStart = 99 }
        };

        var length = ProtocolPdfEntryResolver.ResolveHoldingLength(record, entries);

        Assert.Equal(42.5, length);
    }

    [Fact]
    public void ResolveHoldingLength_falls_back_to_largest_entry_meter()
    {
        var record = new HaltungRecord();
        var entries = new[]
        {
            new ProtocolEntry { MeterStart = 2 },
            new ProtocolEntry { MeterEnd = 7.5 }
        };

        var length = ProtocolPdfEntryResolver.ResolveHoldingLength(record, entries);

        Assert.Equal(7.5, length);
    }
}
