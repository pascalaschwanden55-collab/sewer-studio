using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class VsaFindingProtocolSynchronizerTests
{
    [Fact]
    public void Sync_OhneProtokoll_ErstelltOriginalUndArbeitskopieAusFindings()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Xtf, userEdited: false);
        var findings = new[]
        {
            new VsaFinding
            {
                KanalSchadencode = " BAA ",
                Raw = "  Riss  ",
                MeterStart = 2.5,
                MeterEnd = 4.75,
                MPEG = "00:01:12",
                FotoPath = @"C:\Foto\baa.jpg",
                Quantifizierung1 = "20",
                SchadenlageAnfang = 3,
                SchadenlageEnde = 9
            }
        };

        VsaFindingProtocolSynchronizer.Sync(record, findings);

        Assert.NotNull(record.Protocol);
        Assert.Equal("100-200", record.Protocol!.HaltungId);
        AssertEntry(record.Protocol.Original.Entries.Single());
        AssertEntry(record.Protocol.Current.Entries.Single());
    }

    [Fact]
    public void Sync_BestehendesProtokoll_ErgaenztNurImportierteUndNichtGeloeschteEintraege()
    {
        var imported = new ProtocolEntry
        {
            Code = "BCD",
            Source = ProtocolEntrySource.Imported
        };
        var manual = new ProtocolEntry
        {
            Code = "BCD",
            Source = ProtocolEntrySource.Manual,
            Beschreibung = "manuell"
        };
        var deleted = new ProtocolEntry
        {
            Code = "BCD",
            Source = ProtocolEntrySource.Imported,
            IsDeleted = true
        };
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Original = new ProtocolRevision { Entries = new List<ProtocolEntry> { imported } },
                Current = new ProtocolRevision { Entries = new List<ProtocolEntry> { manual, deleted } }
            }
        };
        var finding = new VsaFinding
        {
            KanalSchadencode = "BCD",
            Raw = "Korrosion",
            MeterStart = 7.25,
            MPEG = "00:00:08",
            FotoPath = @"C:\Foto\bcd.jpg"
        };

        VsaFindingProtocolSynchronizer.Sync(record, new[] { finding });

        Assert.Equal(7.25, imported.MeterStart);
        Assert.Equal("Korrosion", imported.Beschreibung);
        Assert.Equal(TimeSpan.FromSeconds(8), imported.Zeit);
        Assert.Equal(@"C:\Foto\bcd.jpg", Assert.Single(imported.FotoPaths));
        Assert.Equal("manuell", manual.Beschreibung);
        Assert.Null(manual.MeterStart);
        Assert.Null(deleted.MeterStart);
    }

    [Fact]
    public void Sync_HistoryNutztTimestampFallback_BewahrtWerteUndDedupliziertFotos()
    {
        var timestampEntry = new ProtocolEntry
        {
            Code = "ABC",
            Source = ProtocolEntrySource.Imported
        };
        var preservedEntry = new ProtocolEntry
        {
            Code = "BCD",
            Source = ProtocolEntrySource.Imported,
            MeterStart = 1.25,
            MeterEnd = 2.5,
            Beschreibung = "bestehend",
            Mpeg = "00:00:04",
            Zeit = TimeSpan.FromSeconds(4),
            FotoPaths = new List<string> { @"C:\Foto\BCD.JPG" }
        };
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries = new List<ProtocolEntry>
                    {
                        new() { Code = "MAN", Source = ProtocolEntrySource.Manual }
                    }
                },
                History = new List<ProtocolRevision>
                {
                    new() { Entries = new List<ProtocolEntry> { timestampEntry, preservedEntry } }
                }
            }
        };
        var timestamp = new DateTime(2026, 7, 12, 9, 30, 15);
        var findings = new[]
        {
            new VsaFinding { KanalSchadencode = "ABC", Timestamp = timestamp },
            new VsaFinding
            {
                KanalSchadencode = "BCD",
                MeterStart = 9,
                MeterEnd = 12,
                Raw = "neu",
                MPEG = "00:00:20",
                FotoPath = @"c:\foto\bcd.jpg"
            }
        };

        VsaFindingProtocolSynchronizer.Sync(record, findings);

        Assert.Equal(timestamp.TimeOfDay, timestampEntry.Zeit);
        Assert.Equal(1.25, preservedEntry.MeterStart);
        Assert.Equal(2.5, preservedEntry.MeterEnd);
        Assert.Equal("bestehend", preservedEntry.Beschreibung);
        Assert.Equal("00:00:04", preservedEntry.Mpeg);
        Assert.Equal(TimeSpan.FromSeconds(4), preservedEntry.Zeit);
        Assert.Single(preservedEntry.FotoPaths);
    }

    private static void AssertEntry(ProtocolEntry entry)
    {
        Assert.Equal("BAA", entry.Code);
        Assert.Equal("Riss", entry.Beschreibung);
        Assert.Equal(2.5, entry.MeterStart);
        Assert.Equal(4.75, entry.MeterEnd);
        Assert.True(entry.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(12), entry.Zeit);
        Assert.Equal(@"C:\Foto\baa.jpg", Assert.Single(entry.FotoPaths));
        Assert.Equal(ProtocolEntrySource.Imported, entry.Source);
        Assert.Equal("20", entry.CodeMeta?.Parameters["Quantifizierung1"]);
        Assert.Equal("3", entry.CodeMeta?.Parameters["vsa.uhr.von"]);
        Assert.Equal("9", entry.CodeMeta?.Parameters["vsa.uhr.bis"]);
    }
}
