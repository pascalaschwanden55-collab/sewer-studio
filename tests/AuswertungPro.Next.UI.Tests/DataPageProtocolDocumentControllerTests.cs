using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolDocumentControllerTests
{
    [Fact]
    public void EnsureForPdf_keeps_existing_protocol_with_entries()
    {
        var controller = new DataPageProtocolDocumentController();
        var existing = new ProtocolDocument
        {
            Original = new ProtocolRevision { Entries = [new ProtocolEntry { Code = "OLD" }] },
            Current = new ProtocolRevision { Entries = [new ProtocolEntry { Code = "CUR" }] }
        };
        var record = Record("06.24341-35625");
        record.Protocol = existing;

        var result = controller.EnsureForPdf(record, new ProtocolService(), _ => null);

        Assert.Same(existing, result);
        Assert.Same(existing, record.Protocol);
        Assert.Equal("CUR", result.Current.Entries[0].Code);
    }

    [Fact]
    public void EnsureForPdf_rebuilds_empty_existing_protocol_from_vsa_findings()
    {
        var controller = new DataPageProtocolDocumentController();
        var record = Record("06.24341-35625");
        record.Protocol = new ProtocolDocument();
        record.VsaFindings =
        [
            new VsaFinding
            {
                KanalSchadencode = "BAB",
                Raw = "",
                SchadenlageAnfang = 1.25
            }
        ];

        var result = controller.EnsureForPdf(record, new ProtocolService(), code => code == "BAB" ? "Rissbildung" : null);

        Assert.Same(record.Protocol, result);
        Assert.Equal("06.24341-35625", result.HaltungId);
        Assert.Single(result.Current.Entries);
        Assert.Equal("BAB", result.Current.Entries[0].Code);
        Assert.Equal("Rissbildung", result.Current.Entries[0].Beschreibung);
    }

    [Fact]
    public void EnsureForPdf_creates_empty_protocol_when_record_has_no_findings()
    {
        var controller = new DataPageProtocolDocumentController();
        var record = Record("06.24341-35625");

        var result = controller.EnsureForPdf(record, new ProtocolService(), _ => null);

        Assert.Equal("06.24341-35625", result.HaltungId);
        Assert.Empty(result.Current.Entries);
        Assert.Null(record.Protocol);
    }

    private static HaltungRecord Record(string haltung)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", haltung, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
