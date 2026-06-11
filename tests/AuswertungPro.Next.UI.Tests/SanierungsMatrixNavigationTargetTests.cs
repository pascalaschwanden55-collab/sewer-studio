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
}
