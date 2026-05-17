using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Diagnostics;

namespace AuswertungPro.Next.Pipeline.Tests;

public class AiDiagnosticsRowsDiffTests
{
    private static AiDiagnosticEvent E(int tickOffset, string stage = "qwen.raw", string summary = "")
        => new()
        {
            TimestampUtc = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero).AddTicks(tickOffset),
            Stage = stage,
            Summary = string.IsNullOrEmpty(summary) ? $"e{tickOffset}" : summary,
        };

    private sealed class Row
    {
        public DateTimeOffset Key { get; init; }
        public string Summary { get; init; } = "";
    }

    private static Row ToRow(AiDiagnosticEvent e)
        => new() { Key = e.TimestampUtc, Summary = e.Summary };

    private static void Apply(IList<Row> rows, IReadOnlyList<AiDiagnosticEvent> snapshot)
        => AiDiagnosticsRowsDiff.Apply(rows, snapshot, r => r.Key, ToRow);

    [Fact]
    public void LeereListe_LeererSnapshot_NichtsPassiert()
    {
        var rows = new List<Row>();
        Apply(rows, Array.Empty<AiDiagnosticEvent>());
        Assert.Empty(rows);
    }

    [Fact]
    public void LeereListe_VollerSnapshot_AlleEingefuegt()
    {
        var rows = new List<Row>();
        var snap = new[] { E(3), E(2), E(1) }; // newest-first

        Apply(rows, snap);

        Assert.Equal(3, rows.Count);
        Assert.Equal(3, rows[0].Key.UtcTicks - new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero).UtcTicks);
        Assert.Equal(2, rows[1].Key.UtcTicks - new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero).UtcTicks);
        Assert.Equal(1, rows[2].Key.UtcTicks - new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero).UtcTicks);
    }

    [Fact]
    public void NeueEintraegeWerdenObenEingefuegt_BestehendeBleiben()
    {
        var existing = new[] { E(2), E(1) };
        var rows = new List<Row>(existing.Select(ToRow));
        var existingRef0 = rows[0];
        var existingRef1 = rows[1];

        // Zwei neue Events oben drauf
        var snap = new[] { E(4), E(3), E(2), E(1) };
        Apply(rows, snap);

        Assert.Equal(4, rows.Count);
        // E(2) und E(1) sind die SELBEN Row-Instanzen wie vorher → kein Re-Render
        Assert.Same(existingRef0, rows[2]);
        Assert.Same(existingRef1, rows[3]);
    }

    [Fact]
    public void RingbufferEviction_AltesEntfernen()
    {
        var existing = new[] { E(3), E(2), E(1) };
        var rows = new List<Row>(existing.Select(ToRow));

        // E(1) ist aus dem Ringbuffer rausgefallen
        var snap = new[] { E(4), E(3), E(2) };
        Apply(rows, snap);

        Assert.Equal(3, rows.Count);
        Assert.DoesNotContain(rows, r => r.Summary == "e1");
        Assert.Contains(rows, r => r.Summary == "e4");
    }

    [Fact]
    public void Filter_NurMatchingEintraegeBleiben()
    {
        // existing hat 4 Rows, neuer Snapshot ist eine gefilterte Untermenge
        var existing = new[] { E(4), E(3), E(2), E(1) };
        var rows = new List<Row>(existing.Select(ToRow));

        var snap = new[] { E(4), E(2) }; // simulierter Filter
        Apply(rows, snap);

        Assert.Equal(2, rows.Count);
        Assert.Equal("e4", rows[0].Summary);
        Assert.Equal("e2", rows[1].Summary);
    }

    [Fact]
    public void UnveraenderterSnapshot_KeineMutation()
    {
        var existing = new[] { E(3), E(2), E(1) };
        var rows = new ObservableCollection<Row>(existing.Select(ToRow));
        int changes = 0;
        rows.CollectionChanged += (_, _) => changes++;

        var snap = new[] { E(3), E(2), E(1) }; // bit-fuer-bit gleich
        Apply(rows, snap);

        Assert.Equal(3, rows.Count);
        Assert.Equal(0, changes); // KEIN CollectionChanged-Event = kein UI-Rebuild
    }

    [Fact]
    public void EinNeuerEintrag_ExactlyOneInsertEvent()
    {
        var existing = new[] { E(2), E(1) };
        var rows = new ObservableCollection<Row>(existing.Select(ToRow));
        int inserts = 0, removes = 0, resets = 0;
        rows.CollectionChanged += (_, args) =>
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add: inserts++; break;
                case NotifyCollectionChangedAction.Remove: removes++; break;
                case NotifyCollectionChangedAction.Reset: resets++; break;
            }
        };

        var snap = new[] { E(3), E(2), E(1) }; // ein neuer Eintrag oben
        Apply(rows, snap);

        Assert.Equal(1, inserts);
        Assert.Equal(0, removes);
        Assert.Equal(0, resets);
    }

    [Fact]
    public void ListIstIRSL_KeyOf_NullArgsThrowen()
    {
        var rows = new List<Row>();
        var snap = Array.Empty<AiDiagnosticEvent>();
        Assert.Throws<ArgumentNullException>(() =>
            AiDiagnosticsRowsDiff.Apply<Row>(null!, snap, r => r.Key, ToRow));
        Assert.Throws<ArgumentNullException>(() =>
            AiDiagnosticsRowsDiff.Apply(rows, null!, r => r.Key, ToRow));
        Assert.Throws<ArgumentNullException>(() =>
            AiDiagnosticsRowsDiff.Apply(rows, snap, null!, ToRow));
        Assert.Throws<ArgumentNullException>(() =>
            AiDiagnosticsRowsDiff.Apply(rows, snap, r => r.Key, null!));
    }
}
