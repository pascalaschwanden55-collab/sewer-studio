using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class StreckenschadenActionMapperTests
{
    private static StreckenschadenTracker.SegmentAction Action(
        StreckenschadenTracker.SegmentActionType type, string code, double start, double end,
        bool confirmed = false, double? clock = null)
        => new(type, code, clock, start, end, confirmed);

    private static StreckenschadenActionMapper.OpenEntry Open(string code, double start, object? reference = null)
        => new(code, start, reference ?? new object());

    [Fact]
    public void Open_ohne_bestehenden_Eintrag_ergibt_CreateOpen()
    {
        var instr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Open, "BDD", 2.0, 2.0),
            new List<StreckenschadenActionMapper.OpenEntry>());

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.CreateOpen, instr.Kind);
        Assert.Equal("BDD", instr.MainCode);
        Assert.Equal(2.0, instr.StartMeter);
    }

    [Fact]
    public void Open_mit_bereits_offenem_gleichen_Eintrag_ist_idempotent_None()
    {
        var open = new[] { Open("BDD", 2.0) };
        var instr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Open, "BDD", 2.0, 2.0), open);

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.None, instr.Kind);
    }

    [Fact]
    public void Close_findet_passenden_offenen_Eintrag_und_gibt_Referenz_zurueck()
    {
        var marker = new object();
        var open = new[] { Open("BDD", 2.0, marker) };
        var instr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Close, "BDD", 2.0, 5.5, confirmed: true), open);

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.CloseExisting, instr.Kind);
        Assert.Equal(5.5, instr.EndMeter);
        Assert.True(instr.IsConfirmedStrecke);
        Assert.Same(marker, instr.TargetReference);
    }

    [Fact]
    public void Close_ohne_passenden_offenen_Eintrag_ist_None()
    {
        var instr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Close, "BDD", 2.0, 5.5),
            new[] { Open("BBA", 2.0) }); // anderer Code

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.None, instr.Kind);
    }

    [Fact]
    public void Extend_ist_immer_None()
    {
        var instr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Extend, "BDD", 2.0, 3.0),
            new[] { Open("BDD", 2.0) });

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.None, instr.Kind);
    }

    [Fact]
    public void MapAll_filtert_None_heraus()
    {
        var actions = new[]
        {
            Action(StreckenschadenTracker.SegmentActionType.Open, "BDD", 2.0, 2.0),
            Action(StreckenschadenTracker.SegmentActionType.Extend, "BDD", 2.0, 3.0),
            Action(StreckenschadenTracker.SegmentActionType.Close, "BBA", 1.0, 4.0),
        };
        var open = new[] { Open("BBA", 1.0) };

        var instructions = StreckenschadenActionMapper.MapAll(actions, open);

        // Open(BDD) -> CreateOpen, Extend -> None (gefiltert), Close(BBA) -> CloseExisting
        Assert.Equal(2, instructions.Count);
        Assert.Contains(instructions, i => i.Kind == StreckenschadenActionMapper.InstructionKind.CreateOpen);
        Assert.Contains(instructions, i => i.Kind == StreckenschadenActionMapper.InstructionKind.CloseExisting);
    }

    [Fact]
    public void StartMeter_Match_toleriert_kleine_Abweichung()
    {
        // Tracker-StartMeter 2.00, bestehender Eintrag 2.03 -> innerhalb Toleranz -> als gleich erkannt.
        var open = new[] { Open("BDD", 2.03) };
        var closeInstr = StreckenschadenActionMapper.Map(
            Action(StreckenschadenTracker.SegmentActionType.Close, "BDD", 2.00, 6.0), open);

        Assert.Equal(StreckenschadenActionMapper.InstructionKind.CloseExisting, closeInstr.Kind);
    }
}
