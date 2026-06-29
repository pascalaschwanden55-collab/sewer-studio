using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer ImportProtocolApplier.
/// Sichert die drei Faelle: Erstanlage, idempotenter Re-Import (Audit I1) und Revision bei Inhaltsaenderung.
/// </summary>
public sealed class ImportProtocolApplierTests
{
    private readonly ProtocolService _service = new();

    private static HaltungRecord Record(string haltungsname = "H-001")
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", haltungsname, Domain.Models.FieldSource.Legacy, userEdited: false);
        return r;
    }

    private static ProtocolEntry Entry(string code, double? meter)
        => new() { Code = code, MeterStart = meter, FotoPaths = new List<string>() };

    private static List<ProtocolEntry> Entries(params ProtocolEntry[] items)
        => new(items);

    // ------------------------------------------------------------------ Erstanlage

    [Fact]
    public void Protocol_null_wird_per_EnsureProtocol_angelegt()
    {
        var record = Record();
        var entries = Entries(Entry("BAB", 12.5));

        ImportProtocolApplier.Apply(record, entries, _service, "Import (Test)");

        Assert.NotNull(record.Protocol);
        Assert.Single(record.Protocol.Current.Entries);
        Assert.Equal("BAB", record.Protocol.Current.Entries[0].Code);
    }

    [Fact]
    public void Leere_Revision_wird_wie_null_behandelt()
    {
        var record = Record();
        // Leeres Protokoll anlegen (kein Eintrag in Current oder Original)
        record.Protocol = new ProtocolDocument { HaltungId = "H-001" };
        var entries = Entries(Entry("BCE", 47.0));

        ImportProtocolApplier.Apply(record, entries, _service, "Import (Test)");

        // EnsureProtocol ersetzt das leere Dokument
        Assert.NotNull(record.Protocol);
        Assert.Single(record.Protocol.Current.Entries);
    }

    // ------------------------------------------------------------------ Audit I1

    [Fact]
    public void Identischer_ReImport_erzeugt_keine_neue_Revision()
    {
        var record = Record();
        var entries = Entries(Entry("BAB", 12.5), Entry("BCE", 47.9));

        // Erster Import: Erstanlage
        ImportProtocolApplier.Apply(record, entries, _service, "Import (Test)");
        var revisionIdVorher = record.Protocol!.Current.RevisionId;
        var historyCountVorher = record.Protocol.History.Count;

        // Zweiter Import mit identischem Inhalt
        var sameEntries = Entries(Entry("BAB", 12.5), Entry("BCE", 47.9));
        ImportProtocolApplier.Apply(record, sameEntries, _service, "Import (Test)");

        Assert.Equal(revisionIdVorher, record.Protocol.Current.RevisionId);
        Assert.Equal(historyCountVorher, record.Protocol.History.Count);
    }

    // ------------------------------------------------------------------ Neue Revision

    [Fact]
    public void Veraenderter_Inhalt_historisiert_aktuelle_Revision_und_setzt_neuen_Comment()
    {
        var record = Record();
        ImportProtocolApplier.Apply(record, Entries(Entry("BAB", 12.5)), _service, "Import (WinCan DB)");

        var ersteRevisionId = record.Protocol!.Current.RevisionId;

        // Geaenderter Inhalt: anderen Meterstand
        ImportProtocolApplier.Apply(record, Entries(Entry("BAB", 13.0)), _service, "Import (WinCan DB)");

        Assert.NotEqual(ersteRevisionId, record.Protocol.Current.RevisionId);
        Assert.Single(record.Protocol.History);
        Assert.Equal("Import (WinCan DB)", record.Protocol.Current.Comment);
        Assert.Equal(13.0, record.Protocol.Current.Entries[0].MeterStart);
    }

    [Fact]
    public void Comment_Parameter_wird_in_neue_Revision_uebernommen()
    {
        var record = Record();
        ImportProtocolApplier.Apply(record, Entries(Entry("BCD", 0.0)), _service, "Import (IBAK Daten.txt)");
        ImportProtocolApplier.Apply(record, Entries(Entry("BCD", 0.5)), _service, "Import (IBAK Daten.txt)");

        Assert.Equal("Import (IBAK Daten.txt)", record.Protocol!.Current.Comment);
    }
}
