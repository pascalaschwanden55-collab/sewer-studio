using System.Collections.ObjectModel;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventColumnTransferTests
{
    private static CodingEvent Ev(string code, double meter)
        => new()
        {
            MeterAtCapture = meter,
            Entry = new ProtocolEntry { Code = code, Beschreibung = code + " Text", FotoPaths = { "a.png" } }
        };

    [Fact]
    public void Move_entfernt_aus_quelle_und_fuegt_sortiert_in_ziel()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent> { Ev("BAB", 2), Ev("BBA", 9) };
        var moved = src[0];

        var result = CodingEventColumnTransfer.Move(moved, src, target);

        Assert.Same(moved, result);
        Assert.Empty(src);
        Assert.Equal(3, target.Count);
        Assert.Equal(new[] { 2.0, 5.0, 9.0 }, target.Select(e => e.MeterAtCapture)); // nach Meter sortiert
    }

    [Fact]
    public void Copy_laesst_original_und_dupliziert_mit_neuen_ids()
    {
        var src = new ObservableCollection<CodingEvent> { Ev("BCD", 5) };
        var target = new ObservableCollection<CodingEvent>();
        var original = src[0];

        var copy = CodingEventColumnTransfer.Copy(original, target);

        Assert.Single(src);                                   // Original bleibt
        Assert.Single(target);
        Assert.NotEqual(original.EventId, copy.EventId);       // neue EventId
        Assert.NotEqual(original.Entry.EntryId, copy.Entry.EntryId); // neue EntryId
        Assert.Equal("BCD", copy.Entry.Code);
        Assert.Equal(original.Entry.FotoPaths, copy.Entry.FotoPaths);
        Assert.NotSame(original.Entry.FotoPaths, copy.Entry.FotoPaths); // eigene Liste
    }
}
