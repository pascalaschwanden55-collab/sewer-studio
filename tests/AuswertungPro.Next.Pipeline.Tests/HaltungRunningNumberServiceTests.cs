using System.Collections.Generic;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class HaltungRunningNumberServiceTests
{
    [Fact]
    public void AssignNr_numbers_records_one_to_n_in_order()
    {
        var records = new List<HaltungRecord> { new(), new(), new() };

        var changed = HaltungRunningNumberService.AssignNr(records);

        Assert.Equal(3, changed);
        Assert.Equal("1", records[0].GetFieldValue("NR"));
        Assert.Equal("2", records[1].GetFieldValue("NR"));
        Assert.Equal("3", records[2].GetFieldValue("NR"));
    }

    [Fact]
    public void AssignNr_reflects_new_order_after_reorder()
    {
        var a = new HaltungRecord();
        var b = new HaltungRecord();
        HaltungRunningNumberService.AssignNr(new List<HaltungRecord> { a, b });

        // b von Position 2 auf 1 geschoben -> Nummern folgen der Reihenfolge
        HaltungRunningNumberService.AssignNr(new List<HaltungRecord> { b, a });

        Assert.Equal("1", b.GetFieldValue("NR"));
        Assert.Equal("2", a.GetFieldValue("NR"));
    }

    [Fact]
    public void AssignNr_only_reports_changed_entries()
    {
        var records = new List<HaltungRecord> { new(), new() };
        HaltungRunningNumberService.AssignNr(records);

        // Zweiter Aufruf ohne Aenderung -> 0 (kein unnoetiges Dirty/Event beim Oeffnen)
        var changed = HaltungRunningNumberService.AssignNr(records);

        Assert.Equal(0, changed);
    }

    [Fact]
    public void AssignNr_after_delete_closes_the_gap()
    {
        var a = new HaltungRecord();
        var b = new HaltungRecord();
        var c = new HaltungRecord();
        HaltungRunningNumberService.AssignNr(new List<HaltungRecord> { a, b, c });

        // b geloescht -> a=1, c=2 (keine Luecke)
        HaltungRunningNumberService.AssignNr(new List<HaltungRecord> { a, c });

        Assert.Equal("1", a.GetFieldValue("NR"));
        Assert.Equal("2", c.GetFieldValue("NR"));
    }

    [Fact]
    public void AssignNr_overwrites_existing_manual_number()
    {
        var a = new HaltungRecord();
        var b = new HaltungRecord();
        a.SetFieldValue("NR", "99", FieldSource.Manual, userEdited: true);
        b.SetFieldValue("NR", "7", FieldSource.Manual, userEdited: true);

        HaltungRunningNumberService.AssignNr(new List<HaltungRecord> { a, b });

        Assert.Equal("1", a.GetFieldValue("NR"));
        Assert.Equal("2", b.GetFieldValue("NR"));
    }
}
