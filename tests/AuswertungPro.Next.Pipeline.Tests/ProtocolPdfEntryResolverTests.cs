using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfEntryResolverTests
{
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
