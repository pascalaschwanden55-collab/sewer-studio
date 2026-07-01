using System.Collections.Generic;
using System.Text.Json;

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class HaltungRunningNumberServiceTests
{
    [Fact]
    public void Assign_numbers_records_one_to_n_in_order()
    {
        var records = new List<HaltungRecord> { new(), new(), new() };

        var changed = HaltungRunningNumberService.Assign(records);

        Assert.Equal(3, changed);
        Assert.Equal(1, records[0].LaufendeNr);
        Assert.Equal(2, records[1].LaufendeNr);
        Assert.Equal(3, records[2].LaufendeNr);
    }

    [Fact]
    public void Assign_reflects_new_order_after_reorder()
    {
        var a = new HaltungRecord();
        var b = new HaltungRecord();
        HaltungRunningNumberService.Assign(new List<HaltungRecord> { a, b });

        // Reihenfolge getauscht -> neu durchzaehlen
        HaltungRunningNumberService.Assign(new List<HaltungRecord> { b, a });

        Assert.Equal(1, b.LaufendeNr);
        Assert.Equal(2, a.LaufendeNr);
    }

    [Fact]
    public void Assign_only_reports_changed_entries()
    {
        var records = new List<HaltungRecord> { new(), new() };
        HaltungRunningNumberService.Assign(records);

        // Zweiter Aufruf ohne Aenderung -> 0 geaendert (keine unnoetigen Events)
        var changed = HaltungRunningNumberService.Assign(records);

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Assign_after_delete_closes_the_gap()
    {
        var a = new HaltungRecord();
        var b = new HaltungRecord();
        var c = new HaltungRecord();
        HaltungRunningNumberService.Assign(new List<HaltungRecord> { a, b, c });

        // b geloescht -> a=1, c=2
        HaltungRunningNumberService.Assign(new List<HaltungRecord> { a, c });

        Assert.Equal(1, a.LaufendeNr);
        Assert.Equal(2, c.LaufendeNr);
    }

    [Fact]
    public void LaufendeNr_is_not_serialized()
    {
        var record = new HaltungRecord { LaufendeNr = 7 };

        var json = JsonSerializer.Serialize(record);

        Assert.DoesNotContain("LaufendeNr", json);
    }
}
