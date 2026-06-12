using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SanierungsMatrixNavigationTargetTests
{
    [Fact]
    public void FromRecord_liefert_getrimmten_haltungsnamen()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "  07.1028055-10285  ", FieldSource.Manual, userEdited: true);

        Assert.Equal("07.1028055-10285", SanierungsMatrixNavigationTarget.FromRecord(record));
    }

    [Fact]
    public void FromRecord_gibt_null_fuer_fehlende_haltung_zurueck()
    {
        Assert.Null(SanierungsMatrixNavigationTarget.FromRecord(null));
        Assert.Null(SanierungsMatrixNavigationTarget.FromRecord(new HaltungRecord()));
    }

    [Fact]
    public void FindRow_findet_haltung_case_insensitive_und_getrimmt()
    {
        var row = new SanierungMatrixRowVm(new HaltungRecord(), "07.A-08.B", "300", "12.0", 0, _ => { });
        var other = new SanierungMatrixRowVm(new HaltungRecord(), "09.A-10.B", "300", "12.0", 0, _ => { });

        Assert.Same(row, SanierungsMatrixNavigationTarget.FindRow(new[] { other, row }, "  07.a-08.b  "));
    }

    [Fact]
    public void FilterRows_gibt_in_uebersicht_alle_zeilen_zurueck()
    {
        var rows = new[]
        {
            new SanierungMatrixRowVm(new HaltungRecord(), "H1", "300", "12.0", 0, _ => { }),
            new SanierungMatrixRowVm(new HaltungRecord(), "H2", "300", "12.0", 0, _ => { }),
        };

        var filtered = SanierungsMatrixNavigationTarget.FilterRows(rows, "H1", singleHoldingMode: false);

        Assert.Equal(rows, filtered);
    }

    [Fact]
    public void FilterRows_gibt_im_einzelhaltungsmodus_nur_die_zielhaltung_zurueck()
    {
        var target = new SanierungMatrixRowVm(new HaltungRecord(), "H1", "300", "12.0", 0, _ => { });
        var other = new SanierungMatrixRowVm(new HaltungRecord(), "H2", "300", "12.0", 0, _ => { });

        var filtered = SanierungsMatrixNavigationTarget.FilterRows(new[] { other, target }, "h1", singleHoldingMode: true);

        var only = Assert.Single(filtered);
        Assert.Same(target, only);
    }

    [Fact]
    public void FilterRows_bevorzugt_record_referenz_bei_doppeltem_haltungsnamen()
    {
        var firstRecord = new HaltungRecord();
        var secondRecord = new HaltungRecord();
        firstRecord.SetFieldValue("Haltungsname", "H1", FieldSource.Manual, userEdited: true);
        secondRecord.SetFieldValue("Haltungsname", "H1", FieldSource.Manual, userEdited: true);
        var first = new SanierungMatrixRowVm(firstRecord, "H1", "300", "12.0", 0, _ => { });
        var second = new SanierungMatrixRowVm(secondRecord, "H1", "400", "13.0", 0, _ => { });

        var filtered = SanierungsMatrixNavigationTarget.FilterRows(
            new[] { first, second },
            "H1",
            singleHoldingMode: true,
            targetRecord: secondRecord);

        var only = Assert.Single(filtered);
        Assert.Same(second, only);
    }
}
