using AuswertungPro.Next.Application.Ai.Annotation;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class OperateurAnnotationSessionTests
{
    [Fact]
    public void Session_NewlyConstructed_HasEmptyTasks()
    {
        var session = new OperateurAnnotationSession
        {
            CaseId = "case-1",
            VideoPath = "video.mp4",
            PdfPath = "protocol.pdf"
        };
        Assert.Empty(session.Tasks);
        Assert.Null(session.Active);
    }

    [Fact]
    public void CodeTask_DefaultState_IsPending()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        Assert.Equal(CodeTaskState.Pending, task.State);
    }

    [Fact]
    public void CodeTask_TransitionToActive_ClearsBoxAndPreview()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.PreviewReady,
            Box = new BoundingBoxNormalized(0.5, 0.5, 0.1, 0.1)
        };

        task.State = CodeTaskState.Active;
        task.Box = null;
        task.Preview = null;

        Assert.Equal(CodeTaskState.Active, task.State);
        Assert.Null(task.Box);
        Assert.Null(task.Preview);
    }

    [Fact]
    public void CodeTask_Committed_SetsSampleIdAndUtc()
    {
        var task = new CodeTask
        {
            Code = "BAB B",
            Meterstand = 12.3,
            State = CodeTaskState.Committed,
            CommittedSampleId = "sample-xyz",
            CommittedUtc = System.DateTime.UtcNow
        };

        Assert.Equal("sample-xyz", task.CommittedSampleId);
        Assert.NotNull(task.CommittedUtc);
    }

    [Fact]
    public void CodeTask_Skipped_StoresUserReason()
    {
        var task = new CodeTask
        {
            Code = "BBB Z",
            Meterstand = 24.1,
            State = CodeTaskState.Skipped,
            UserReason = "Frame komplett unscharf"
        };

        Assert.Equal(CodeTaskState.Skipped, task.State);
        Assert.Equal("Frame komplett unscharf", task.UserReason);
    }

    [Fact]
    public void CodeTaskState_AllValuesDefined()
    {
        var states = new[]
        {
            CodeTaskState.Pending,
            CodeTaskState.Active,
            CodeTaskState.PreviewReady,
            CodeTaskState.Committed,
            CodeTaskState.Skipped,
            CodeTaskState.Rejected,
            CodeTaskState.Error
        };
        Assert.Equal(7, states.Length);
    }

    [Fact]
    public void Session_Active_PointsToTaskInTasks()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        var session = new OperateurAnnotationSession { CaseId = "c1" };
        session.Tasks.Add(task);
        session.Active = task;

        Assert.Same(task, session.Active);
        Assert.Single(session.Tasks);
    }

    [Fact]
    public void MoveToCode_SetsActiveAndState()
    {
        var session = NewSessionWith("BAB B", "BBB Z");

        session.MoveToCode(session.Tasks[0]);

        Assert.Same(session.Tasks[0], session.Active);
        Assert.Equal(CodeTaskState.Active, session.Tasks[0].State);
    }

    [Fact]
    public void MoveToCode_PreviousActiveWithoutCommit_FallsBackToPending()
    {
        var session = NewSessionWith("BAB B", "BBB Z");
        var first = session.Tasks[0];
        var second = session.Tasks[1];

        session.MoveToCode(first);
        first.Box = new BoundingBoxNormalized(0.5, 0.5, 0.1, 0.1);
        first.State = CodeTaskState.PreviewReady;

        session.MoveToCode(second);

        Assert.Equal(CodeTaskState.Pending, first.State);
        Assert.Null(first.Box);
        Assert.Same(second, session.Active);
        Assert.Equal(CodeTaskState.Active, second.State);
    }

    [Fact]
    public void MoveToCode_DoesNotResetCommittedTasks()
    {
        var session = NewSessionWith("BAB B", "BBB Z");
        var first = session.Tasks[0];
        var second = session.Tasks[1];

        session.MoveToCode(first);
        session.MarkActiveCommitted("sample-1", DateTime.UtcNow);
        Assert.Equal(CodeTaskState.Committed, first.State);

        session.MoveToCode(second);

        Assert.Equal(CodeTaskState.Committed, first.State);
        Assert.Equal("sample-1", first.CommittedSampleId);
    }

    [Fact]
    public void MoveToCode_TaskNotInSession_Throws()
    {
        var session = NewSessionWith("BAB B");
        var foreign = new CodeTask { Code = "BBB Z", Meterstand = 5 };

        Assert.Throws<ArgumentException>(() => session.MoveToCode(foreign));
    }

    [Fact]
    public void MarkActiveCommitted_SetsSampleIdAndUtc()
    {
        var session = NewSessionWith("BAB B");
        session.MoveToCode(session.Tasks[0]);

        var ts = new DateTime(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
        session.MarkActiveCommitted("sample-x", ts);

        Assert.Equal(CodeTaskState.Committed, session.Tasks[0].State);
        Assert.Equal("sample-x", session.Tasks[0].CommittedSampleId);
        Assert.Equal(ts, session.Tasks[0].CommittedUtc);
    }

    [Fact]
    public void MarkActive_NoActive_Throws()
    {
        var session = NewSessionWith("BAB B");
        Assert.Throws<InvalidOperationException>(() =>
            session.MarkActiveCommitted("x", DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() => session.MarkActiveSkipped("r"));
        Assert.Throws<InvalidOperationException>(() => session.MarkActiveRejected("r"));
    }

    [Fact]
    public void FindFirstPending_ReturnsFirstPending_OrNullIfAllDone()
    {
        var session = NewSessionWith("BAB B", "BBB Z", "BCD");
        session.MoveToCode(session.Tasks[0]);
        session.MarkActiveCommitted("s1", DateTime.UtcNow);

        var nextFirst = session.FindFirstPending();
        Assert.Same(session.Tasks[1], nextFirst);

        // Alle erledigen
        session.MoveToCode(session.Tasks[1]);
        session.MarkActiveSkipped("blur");
        session.MoveToCode(session.Tasks[2]);
        session.MarkActiveCommitted("s3", DateTime.UtcNow);

        Assert.Null(session.FindFirstPending());
    }

    [Fact]
    public void MarkActiveCommitted_OnTerminal_Throws()
    {
        var session = NewSessionWith("BAB B");
        session.MoveToCode(session.Tasks[0]);
        session.MarkActiveCommitted("s1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            session.MarkActiveCommitted("s2", DateTime.UtcNow));
    }

    [Fact]
    public void MarkActiveSkipped_OnTerminal_Throws()
    {
        var session = NewSessionWith("BAB B");
        session.MoveToCode(session.Tasks[0]);
        session.MarkActiveCommitted("s1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => session.MarkActiveSkipped("blur"));
    }

    [Fact]
    public void MarkActiveRejected_OnTerminal_Throws()
    {
        var session = NewSessionWith("BAB B");
        session.MoveToCode(session.Tasks[0]);
        session.MarkActiveSkipped("blur");

        Assert.Throws<InvalidOperationException>(() => session.MarkActiveRejected("nope"));
    }

    [Fact]
    public void CodeTask_StateChange_FiresPropertyChanged()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        var changes = new System.Collections.Generic.List<string?>();
        task.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        task.State = CodeTaskState.Active;
        task.State = CodeTaskState.PreviewReady;
        task.State = CodeTaskState.PreviewReady;   // gleiches Value -> kein Event

        Assert.Equal(new[] { nameof(CodeTask.State), nameof(CodeTask.State) }, changes);
    }

    [Fact]
    public void CodeTask_BoxAndPreview_FirePropertyChanged()
    {
        var task = new CodeTask { Code = "BAB B", Meterstand = 12.3 };
        var changes = new System.Collections.Generic.List<string?>();
        task.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        task.Box = new BoundingBoxNormalized(0.5, 0.5, 0.1, 0.1);
        task.Preview = new MaskPreview("rle", "v1", "[]", 100, 100, 10, 0.5,
            System.TimeSpan.Zero, null);

        Assert.Contains(nameof(CodeTask.Box), changes);
        Assert.Contains(nameof(CodeTask.Preview), changes);
    }

    [Fact]
    public void IsTerminal_ReturnsTrueForCommittedSkippedRejected()
    {
        Assert.True(OperateurAnnotationSession.IsTerminal(CodeTaskState.Committed));
        Assert.True(OperateurAnnotationSession.IsTerminal(CodeTaskState.Skipped));
        Assert.True(OperateurAnnotationSession.IsTerminal(CodeTaskState.Rejected));
        Assert.False(OperateurAnnotationSession.IsTerminal(CodeTaskState.Pending));
        Assert.False(OperateurAnnotationSession.IsTerminal(CodeTaskState.Active));
        Assert.False(OperateurAnnotationSession.IsTerminal(CodeTaskState.PreviewReady));
        Assert.False(OperateurAnnotationSession.IsTerminal(CodeTaskState.Error));
    }

    [Fact]
    public void FindNextPending_SkipsCommittedTasksAfterActive()
    {
        var session = NewSessionWith("A", "B", "C");
        // A=Active, B=Committed (vorgesetzt), C=Pending
        session.MoveToCode(session.Tasks[0]);
        session.Tasks[1].State = CodeTaskState.Committed;

        var next = session.FindNextPending();
        Assert.Same(session.Tasks[2], next);
    }

    private static OperateurAnnotationSession NewSessionWith(params string[] codes)
    {
        var session = new OperateurAnnotationSession { CaseId = "c1" };
        for (var i = 0; i < codes.Length; i++)
            session.Tasks.Add(new CodeTask { Code = codes[i], Meterstand = (i + 1) * 10.0 });
        return session;
    }
}
