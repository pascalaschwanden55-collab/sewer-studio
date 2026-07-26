using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Training;

/// <summary>
/// Tests fuer die Auswahl-Logik des KB-Nachhol-Laufs ("Gold in KB nachholen").
/// Kernregel: nur menschlich bestaetigtes Gold (Status=Approved), das noch NICHT erfolgreich
/// indexiert ist (KbIndexState != Indexed), wird aufgegriffen. Rejected/Removed/New bleiben
/// draussen — Negativ-/Roh-Samples gehoeren nicht in die positive Retrieval-KB.
/// </summary>
public sealed class KbReconcilePlannerTests
{
    private static TrainingSample Make(
        string id,
        TrainingSampleStatus status,
        KbIndexState kb,
        bool eligible = true)
        => new()
        {
            SampleId = id,
            CaseId = "case",
            Code = "BAB",
            Beschreibung = "Riss laengs bei 3 Uhr, deutlich",
            Status = status,
            KbIndexState = kb,
            TrainingEligible = eligible,
        };

    [Fact]
    public void SelectPending_ApprovedNotIndexed_IsSelected()
    {
        var samples = new List<TrainingSample>
        {
            Make("a", TrainingSampleStatus.Approved, KbIndexState.None),
            Make("b", TrainingSampleStatus.Approved, KbIndexState.Error),
            Make("c", TrainingSampleStatus.Approved, KbIndexState.Pending),
        };

        var pending = KbReconcilePlanner.SelectPending(samples);

        Assert.Equal(3, pending.Count);
        Assert.Equal(new[] { "a", "b", "c" }, pending.Select(s => s.SampleId).OrderBy(x => x));
    }

    [Fact]
    public void SelectPending_AlreadyIndexed_IsSkipped()
    {
        var samples = new List<TrainingSample>
        {
            Make("indexed", TrainingSampleStatus.Approved, KbIndexState.Indexed),
        };

        Assert.Empty(KbReconcilePlanner.SelectPending(samples));
    }

    [Fact]
    public void SelectPending_Skipped_IsNotRetried()
    {
        // Bewusst/dauerhaft verworfen (Eval/nicht index-wuerdig) -> NICHT erneut versuchen,
        // sonst liefe es bei jedem Nachhol-Lauf wieder ins Leere.
        var samples = new List<TrainingSample>
        {
            Make("eval_or_unworthy", TrainingSampleStatus.Approved, KbIndexState.Skipped),
        };

        Assert.Empty(KbReconcilePlanner.SelectPending(samples));
    }

    [Theory]
    [InlineData(TrainingSampleStatus.Rejected)]
    [InlineData(TrainingSampleStatus.Removed)]
    [InlineData(TrainingSampleStatus.New)]
    public void SelectPending_NonApproved_IsSkipped(TrainingSampleStatus status)
    {
        // Selbst mit KbIndexState=None duerfen Nicht-Approved-Samples NICHT in die positive KB.
        var samples = new List<TrainingSample> { Make("x", status, KbIndexState.None) };
        Assert.Empty(KbReconcilePlanner.SelectPending(samples));
    }

    [Fact]
    public void SelectPending_Draft_IsSkipped_EvenWithPendingState()
    {
        // Workbench-Entwuerfe (Status=Draft, KbIndexState=Pending) duerfen vom Nachhol-Lauf
        // NICHT nachgeholt werden — sie werden erst nach der Masken-Reparatur zu Gold.
        var samples = new List<TrainingSample>
        {
            Make("entwurf", TrainingSampleStatus.Draft, KbIndexState.Pending),
        };

        Assert.Empty(KbReconcilePlanner.SelectPending(samples));
    }

    [Fact]
    public void SelectPending_NullInput_ReturnsEmpty()
    {
        Assert.Empty(KbReconcilePlanner.SelectPending(null!));
    }

    [Fact]
    public void SelectPending_MixedRealistic_PicksOnlyWaitingGold()
    {
        var samples = new List<TrainingSample>
        {
            Make("gold_wait1", TrainingSampleStatus.Approved, KbIndexState.None),
            Make("gold_wait2", TrainingSampleStatus.Approved, KbIndexState.Error),
            Make("gold_done",  TrainingSampleStatus.Approved, KbIndexState.Indexed),
            Make("negativ",    TrainingSampleStatus.Rejected, KbIndexState.None),
            Make("roh",        TrainingSampleStatus.New,      KbIndexState.None),
        };

        var pending = KbReconcilePlanner.SelectPending(samples);

        Assert.Equal(new[] { "gold_wait1", "gold_wait2" },
            pending.Select(s => s.SampleId).OrderBy(x => x));
    }

    [Fact]
    public void CountPending_ReportsTotalAndEligibleHonestly()
    {
        var samples = new List<TrainingSample>
        {
            Make("a", TrainingSampleStatus.Approved, KbIndexState.None, eligible: true),
            Make("b", TrainingSampleStatus.Approved, KbIndexState.None, eligible: false),
            Make("c", TrainingSampleStatus.Approved, KbIndexState.Error, eligible: true),
            Make("done", TrainingSampleStatus.Approved, KbIndexState.Indexed, eligible: true),
            Make("skip", TrainingSampleStatus.Approved, KbIndexState.Skipped, eligible: true),
            Make("neg", TrainingSampleStatus.Rejected, KbIndexState.None, eligible: true),
        };

        var (total, eligible) = KbReconcilePlanner.CountPending(samples);

        Assert.Equal(3, total);     // a, b, c (done=Indexed raus, skip=Skipped raus, neg=Rejected raus)
        Assert.Equal(2, eligible);  // a, c
    }
}
